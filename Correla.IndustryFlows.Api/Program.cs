using Correla.IndustryFlows.Dtc;
using Correla.IndustryFlows.Dtc.DependencyInjection;
using Correla.IndustryFlows.Dtc.Parsing;
using Correla.IndustryFlows.Dtc.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Read bundle path from configuration; relative paths resolve against ContentRootPath.
var bundlePath = builder.Configuration["Dtc:BundlePath"]
    ?? throw new InvalidOperationException("Configuration key 'Dtc:BundlePath' is required.");

if (!Path.IsPathRooted(bundlePath))
{
    bundlePath = Path.Combine(builder.Environment.ContentRootPath, bundlePath);
}

builder.Services.AddDtcParser(opts =>
{
    opts.BundlePath = bundlePath;
    opts.RegisterDefaultPredicates = true;
});

// JSON: camelCase property names, string enum converter, and populate-mode for
// read-only collection properties (List<T> / Dictionary<K,V> with no setter).
// Populate-mode is required so that GroupInstance.Children and GroupInstance.Fields
// are populated when the generate endpoint deserialises an incoming DtcFile tree.
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    opts.SerializerOptions.PreferredObjectCreationHandling =
        System.Text.Json.Serialization.JsonObjectCreationHandling.Populate;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ---- Flows API ----

// POST /flows/parse
// Accepts multipart/form-data with a field named 'file' containing the raw DTC flat file.
// Returns ProcessingResult as JSON (200 on success, 400 on fatal failure).
app.MapPost("/flows/parse", async (IFormFile file, DtcProcessor processor, CancellationToken ct) =>
{
    await using var stream = file.OpenReadStream();
    var result = await processor.ProcessAsync(stream, ct: ct);

    return result.Success
        ? Results.Ok(result)
        : Results.BadRequest(result);
})
.WithName("ParseFlow")
.WithSummary("Parse and validate a DTC flat file, returning a structured JSON result.")
.DisableAntiforgery();

// POST /flows/generate
// Accepts a JSON body containing the envelope metadata and GroupInstance tree to serialise.
// Returns the DTC flat file as text/plain, or 422 with findings on validation failure.
app.MapPost("/flows/generate", (
    GenerateRequest request,
    ISchemaRegistry registry) =>
{
    if (!registry.TryGet(request.FlowId, request.FlowVersion, out var schema) || schema is null)
    {
        return Results.BadRequest(
            new { error = $"No schema found for flow '{request.FlowId}' version '{request.FlowVersion}'." });
    }

    var envelope = new Envelope(
        request.FlowId, request.FlowVersion, request.Sender, request.Recipient);

    var result = DtcFileWriter.Write(request.File, schema, envelope, request.FileReference);

    if (!result.Success)
    {
        return Results.UnprocessableEntity(new { findings = result.Findings });
    }

    return Results.Text(result.Content!, "text/plain");
})
.WithName("GenerateFlow")
.WithSummary("Generate a DTC flat file from a validated JSON group-instance tree.");

app.Run();

/// <summary>
/// Request body for the POST /flows/generate endpoint.
/// </summary>
/// <param name="FlowId">DTC flow identifier (e.g. <c>D0010</c>).</param>
/// <param name="FlowVersion">Flow catalogue version (e.g. <c>002</c>).</param>
/// <param name="Sender">Sending participant role code.</param>
/// <param name="Recipient">Receiving participant role code.</param>
/// <param name="FileReference">File reference written into the ZHV header (e.g. <c>FILE-001</c>).</param>
/// <param name="File">The parsed DTC file tree to serialise back to flat-file format.</param>
public sealed record GenerateRequest(
    string FlowId,
    string FlowVersion,
    string Sender,
    string Recipient,
    string FileReference,
    DtcFile File);

/// <summary>Exposes the application entry point for <c>WebApplicationFactory</c> in integration tests.</summary>
public partial class Program { }
