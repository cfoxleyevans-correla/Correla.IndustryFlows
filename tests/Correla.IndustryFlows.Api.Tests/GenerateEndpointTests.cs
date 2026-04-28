using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Correla.IndustryFlows.Api.Tests;

/// <summary>
/// Integration tests for the POST /flows/generate endpoint.
/// Sends a JSON <c>GenerateRequest</c> and asserts on the DTC flat-file text
/// (or error body) returned by the endpoint.
/// </summary>
public sealed class GenerateEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // ---- Minimal valid D0010 tree ----
    // 026 (min=1) requires 028 (min=1). Groups 030 and 032 are optional (min=0),
    // so the smallest valid tree is ROOT → 026 → 028.

    private const string MinimalD0010Json = """
        {
          "flowId": "D0010",
          "flowVersion": "002",
          "sender": "NHHDA",
          "recipient": "UDMS",
          "fileReference": "API-001",
          "file": {
            "root": {
              "groupCode": "ROOT",
              "lineNumber": 0,
              "fields": {},
              "children": [
                {
                  "groupCode": "026",
                  "lineNumber": 2,
                  "fields": { "J0003": "2000000000015", "J0022": "V" },
                  "children": [
                    {
                      "groupCode": "028",
                      "lineNumber": 3,
                      "fields": { "J0004": "S95A123456", "J0171": "R" },
                      "children": []
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

    // ---- Happy path ----

    [Fact]
    public async Task GenerateEndpoint_ValidD0010Tree_Returns200WithZhvHeader()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(MinimalD0010Json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var flat = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("ZHV|", flat, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEndpoint_ValidD0010Tree_ReturnsTextPlainContentType()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(MinimalD0010Json, Encoding.UTF8, "application/json"));

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("text/plain", contentType);
    }

    [Fact]
    public async Task GenerateEndpoint_ValidD0010Tree_EnvelopeMatchesRequest()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(MinimalD0010Json, Encoding.UTF8, "application/json"));

        var flat = await response.Content.ReadAsStringAsync();

        // ZHV line should contain the flow ID, version, sender, and recipient.
        Assert.Contains("D0010", flat, StringComparison.Ordinal);
        Assert.Contains("002", flat, StringComparison.Ordinal);
        Assert.Contains("NHHDA", flat, StringComparison.Ordinal);
        Assert.Contains("UDMS", flat, StringComparison.Ordinal);
        Assert.Contains("API-001", flat, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEndpoint_ValidD0010Tree_EndsWithZptLine()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(MinimalD0010Json, Encoding.UTF8, "application/json"));

        var flat = await response.Content.ReadAsStringAsync();

        // Every generated DTC file must end with a ZPT trailer line.
        Assert.Contains("ZPT|", flat, StringComparison.Ordinal);
    }

    // ---- Unknown flow ----

    [Fact]
    public async Task GenerateEndpoint_UnknownFlow_Returns400()
    {
        const string json = """
            {
              "flowId": "D9999",
              "flowVersion": "001",
              "sender": "NHHDA",
              "recipient": "UDMS",
              "fileReference": "API-001",
              "file": {
                "root": { "groupCode": "ROOT", "lineNumber": 0, "fields": {}, "children": [] }
              }
            }
            """;

        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Invalid tree (missing required fields) ----

    [Fact]
    public async Task GenerateEndpoint_MissingRequiredField_Returns422WithFindings()
    {
        // 026 is present but J0003 (MPAN Core, required) is absent.
        const string json = """
            {
              "flowId": "D0010",
              "flowVersion": "002",
              "sender": "NHHDA",
              "recipient": "UDMS",
              "fileReference": "API-001",
              "file": {
                "root": {
                  "groupCode": "ROOT",
                  "lineNumber": 0,
                  "fields": {},
                  "children": [
                    {
                      "groupCode": "026",
                      "lineNumber": 2,
                      "fields": { "J0022": "V" },
                      "children": []
                    }
                  ]
                }
              }
            }
            """;

        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/flows/generate",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var findings = body.GetProperty("findings");
        Assert.NotEqual(JsonValueKind.Null, findings.ValueKind);
        Assert.NotEqual(0, findings.GetArrayLength());
    }

    // ---- Round-trip ----

    [Fact]
    public async Task GenerateEndpoint_ParseThenGenerate_ProducesValidFlatFile()
    {
        // Parse a valid D0010 file, take the parsed tree, then feed it back to generate.
        var originalDtc =
            "ZHV|FILE-H001|D0010|002|NHHDA|UDMS|20260415\r\n" +
            "026|2000000000015|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "ZPT|3|\r\n";

        var client = factory.CreateClient();

        // Step 1: parse
        var bytes = Encoding.UTF8.GetBytes(originalDtc);
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent();
        form.Add(part, "file", "original.dtc");

        var parseResponse = await client.PostAsync("/flows/parse", form);
        Assert.Equal(HttpStatusCode.OK, parseResponse.StatusCode);

        var parseBody = await parseResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(parseBody.GetProperty("success").GetBoolean());

        // Step 2: build generate request from parsed tree.
        var parsedFile = parseBody.GetProperty("parsed").GetRawText();

        var generateJson = $$"""
            {
              "flowId": "D0010",
              "flowVersion": "002",
              "sender": "NHHDA",
              "recipient": "UDMS",
              "fileReference": "ROUND-001",
              "file": {{parsedFile}}
            }
            """;

        var generateResponse = await client.PostAsync(
            "/flows/generate",
            new StringContent(generateJson, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

        var generated = await generateResponse.Content.ReadAsStringAsync();
        Assert.StartsWith("ZHV|", generated, StringComparison.Ordinal);
        Assert.Contains("ROUND-001", generated, StringComparison.Ordinal);
    }
}

