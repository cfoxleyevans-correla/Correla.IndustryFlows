using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Correla.IndustryFlows.Api.Tests;

/// <summary>
/// Integration tests for the POST /flows/parse endpoint.
/// Sends raw DTC flat-file content as multipart/form-data and asserts on the
/// JSON <c>ProcessingResult</c> returned by the endpoint.
/// </summary>
public sealed class ParseEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ---- Helper ----

    /// <summary>
    /// Wraps <paramref name="content"/> in a multipart/form-data request matching
    /// the field name the endpoint expects (<c>file</c>).
    /// </summary>
    private static MultipartFormDataContent BuildFileUpload(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var form = new MultipartFormDataContent();
        form.Add(part, "file", "test.dtc");
        return form;
    }

    // ---- Happy path ----

    [Fact]
    public async Task ParseEndpoint_ValidD0010_Returns200WithSuccessTrue()
    {
        // A minimal D0010 file that should parse cleanly.
        var dtcFile =
            "ZHV|FILE-H001|D0010|002|NHHDA|UDMS|20260415\r\n" +
            "026|2000000000015|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "030|01|20260415093000|045231||||T|\r\n" +
            "032|01|T|\r\n" +
            "ZPT|5|\r\n";

        var client = factory.CreateClient();

        var response = await client.PostAsync("/flows/parse", BuildFileUpload(dtcFile));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Equal("D0010", body.GetProperty("envelope").GetProperty("flowId").GetString());
    }

    [Fact]
    public async Task ParseEndpoint_ValidD0010_ReturnsParsedTree()
    {
        var dtcFile =
            "ZHV|FILE-H001|D0010|002|NHHDA|UDMS|20260415\r\n" +
            "026|2000000000015|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "030|01|20260415093000|045231||||T|\r\n" +
            "032|01|T|\r\n" +
            "ZPT|5|\r\n";

        var client = factory.CreateClient();

        var response = await client.PostAsync("/flows/parse", BuildFileUpload(dtcFile));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        // Parsed tree must be present.
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("parsed").ValueKind);
    }

    // ---- Unknown flow ----

    [Fact]
    public async Task ParseEndpoint_UnknownFlow_Returns400WithSuccessFalse()
    {
        var dtcFile = "ZHV|FILE-001|D9999|001|NHHDA|UDMS|\r\n";

        var client = factory.CreateClient();

        var response = await client.PostAsync("/flows/parse", BuildFileUpload(dtcFile));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("failureReason").GetString()));
    }

    // ---- Empty / malformed stream ----

    [Fact]
    public async Task ParseEndpoint_EmptyFile_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/flows/parse", BuildFileUpload(string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ParseEndpoint_MissingZhvLine_Returns400()
    {
        // Content that does not start with ZHV cannot be parsed.
        var dtcFile = "026|2000000000015|V|\r\n";

        var client = factory.CreateClient();

        var response = await client.PostAsync("/flows/parse", BuildFileUpload(dtcFile));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

