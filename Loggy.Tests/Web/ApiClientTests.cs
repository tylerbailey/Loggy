using System.Net;
using System.Text;
using System.Text.Json;
using Loggy.Models;
using Loggy.Models.Gemini;
using Loggy.Web.ApiClients;
using Microsoft.AspNetCore.Components.Forms;
using Moq;
using Moq.Protected;

namespace Loggy.Web.Tests.ApiClients;

// ---------------------------------------------------------------------------
// LogUploadApiClient
// ---------------------------------------------------------------------------

public class LogUploadApiClientTests
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Builds a client whose inner handler returns the given status + body.
    /// </summary>
    private static (LogUploadApiClient client, Mock<HttpMessageHandler> handler)
        Build(HttpStatusCode status, string body)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://apiservice") };
        return (new LogUploadApiClient(http), handlerMock);
    }

    private static Mock<IBrowserFile> MakeBrowserFile(string name = "log.json", string contentType = "application/json", long size = 100)
    {
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns(name);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Size).Returns(size);
        fileMock.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("{}")));
        return fileMock;
    }

    // -------------------------------------------------------------------------
    // URL construction
    // -------------------------------------------------------------------------

    [Theory]
    // schemaType 0 = Serilog, sortOption 0 = ByException  → /api/SerilogUpload/SortByException
    [InlineData(0, 0, "/api/SerilogUpload/SortByException")]
    // schemaType 0 = Serilog, sortOption 1 = ByTimeStamp  → /api/SerilogUpload/SortByTimeStamp
    [InlineData(0, 1, "/api/SerilogUpload/SortByTimeStamp")]
    // schemaType 1 = NLog, sortOption 0 = ByException     → /api/NLogUpload/SortByException
    [InlineData(1, 0, "/api/NLogUpload/SortByException")]
    public async Task UploadLogAsync_BuildsCorrectUrl(int schemaType, int sortOption, string expectedPath)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, List<SeriLogEvent>>());
        var (client, handlerMock) = Build(HttpStatusCode.OK, payload);

        await client.UploadLogAsync(MakeBrowserFile().Object, schemaType, sortOption, modelOption: 0);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.PathAndQuery == expectedPath),
            ItExpr.IsAny<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Multipart request
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadLogAsync_SendsMultipartFormData()
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, List<SeriLogEvent>>());
        var (client, handlerMock) = Build(HttpStatusCode.OK, payload);

        await client.UploadLogAsync(MakeBrowserFile().Object, schemaType: 0, sortOption: 0, modelOption: 0);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Content is MultipartFormDataContent),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task UploadLogAsync_UsesFilenameFromBrowserFile()
    {
        string? capturedFileName = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                if (req.Content is MultipartFormDataContent multipart)
                {
                    foreach (var part in multipart)
                        capturedFileName = part.Headers.ContentDisposition?.FileName;
                }
                await Task.CompletedTask;
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://apiservice") };
        var client = new LogUploadApiClient(http);

        await client.UploadLogAsync(MakeBrowserFile("mylog.json").Object, 0, 0, 0);

        Assert.Equal("mylog.json", capturedFileName);
    }

    // -------------------------------------------------------------------------
    // Response handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UploadLogAsync_SuccessResponse_DeserializesEvents()
    {
        var events = new Dictionary<string, List<SeriLogEvent>>
        {
            ["NullReferenceException"] = new()
            {
                new() { Id = 1, Level = "Error", Message = "crash" }
            }
        };
        var (client, _) = Build(HttpStatusCode.OK, JsonSerializer.Serialize(events));

        var result = await client.UploadLogAsync(MakeBrowserFile().Object, 0, 0, 0);

        Assert.Single(result);
        Assert.True(result.ContainsKey("NullReferenceException"));
        Assert.Equal("Error", result["NullReferenceException"][0].Level);
    }

    [Fact]
    public async Task UploadLogAsync_EmptyJsonResponse_ReturnsEmptyDictionary()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{}");
        var result = await client.UploadLogAsync(MakeBrowserFile().Object, 0, 0, 0);
        Assert.Empty(result);
    }

    [Fact]
    public async Task UploadLogAsync_ServerReturns500_ThrowsHttpRequestException()
    {
        var (client, _) = Build(HttpStatusCode.InternalServerError, "error");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UploadLogAsync(MakeBrowserFile().Object, 0, 0, 0));
    }

    [Fact]
    public async Task UploadLogAsync_ServerReturns400_ThrowsHttpRequestException()
    {
        var (client, _) = Build(HttpStatusCode.BadRequest, "bad request");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UploadLogAsync(MakeBrowserFile().Object, 0, 0, 0));
    }
}

// ---------------------------------------------------------------------------
// AnalysisApiClient
// ---------------------------------------------------------------------------

public class AnalysisApiClientTests
{
    private static (AnalysisApiClient client, Mock<HttpMessageHandler> handler)
        Build(HttpStatusCode status, string body)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://apiservice") };
        return (new AnalysisApiClient(http), handlerMock);
    }

    /// <summary>
    /// Wraps a LogAnalysis JSON string in the Gemini response envelope the
    /// controller returns.
    /// </summary>
    private static string WrapInGeminiEnvelope(string innerJson) =>
        JsonSerializer.Serialize(new GeminiResponse
        {
            Candidates =
            [
                new GeminiCandidate
                {
                    Content = new GeminiContentPart
                    {
                        Parts = [new GeminiPart { Text = innerJson }]
                    }
                }
            ]
        });

    private static Dictionary<string, List<SeriLogEvent>> SampleLogs() =>
        new()
        {
            ["app"] = [new() { Id = 1, Level = "Error", Message = "oops" }]
        };

    // -------------------------------------------------------------------------
    // URL construction — modelOption drives the route
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, "/api/GeminiAPI/Query")]   // 0 = Gemini
    [InlineData(1, "/api/CustomAPI/Query")]   // 1 = Custom
    public async Task AnalyzeLogsAsync_BuildsCorrectUrl(int modelOption, string expectedPath)
    {
        var (client, handlerMock) = Build(HttpStatusCode.OK, WrapInGeminiEnvelope("{}"));

        await client.AnalyzeLogsAsync(SampleLogs(), modelOption);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.PathAndQuery == expectedPath),
            ItExpr.IsAny<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Response parsing
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeLogsAsync_ValidGeminiResponse_ReturnsLogAnalysis()
    {
        var analysis = new LogAnalysis
        {
            Summary = "1 error found",
            TimeRange = "10:00–10:01",
            Patterns =
            [
                new LogPattern
                {
                    Title = "DB failure",
                    Severity = "High",
                    Description = "Connection dropped",
                    Recommendation = "Restart DB",
                    RelatedEventIds = [1]
                }
            ],
            ErrorCounts = new ErrorCounts { Errors = 1, Info = 5 }
        };

        var envelope = WrapInGeminiEnvelope(JsonSerializer.Serialize(analysis));
        var (client, _) = Build(HttpStatusCode.OK, envelope);

        var result = await client.AnalyzeLogsAsync(SampleLogs(), modelOption: 0);

        Assert.Equal("1 error found", result.Summary);
        Assert.Equal("10:00–10:01", result.TimeRange);
        Assert.Single(result.Patterns);
        Assert.Equal("High", result.Patterns[0].Severity);
        Assert.Equal(1, result.ErrorCounts.Errors);
    }

    [Fact]
    public async Task AnalyzeLogsAsync_EmptyPatternsInAnalysis_ReturnsEmptyPatternsList()
    {
        var analysis = new LogAnalysis { Summary = "all clear", Patterns = [] };
        var (client, _) = Build(HttpStatusCode.OK, WrapInGeminiEnvelope(JsonSerializer.Serialize(analysis)));

        var result = await client.AnalyzeLogsAsync(SampleLogs(), 0);

        Assert.Empty(result.Patterns);
    }

    [Fact]
    public async Task AnalyzeLogsAsync_NoCandidatesInResponse_ReturnsDefaultLogAnalysis()
    {
        // Gemini returns no candidates — FirstOrDefault() falls through to "No response"
        // which cannot deserialise to LogAnalysis, so we get a default instance
        var emptyEnvelope = JsonSerializer.Serialize(new GeminiResponse { Candidates = [] });
        var (client, _) = Build(HttpStatusCode.OK, emptyEnvelope);

        var result = await client.AnalyzeLogsAsync(SampleLogs(), 0);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Summary);
    }

    [Fact]
    public async Task AnalyzeLogsAsync_PostsJsonBody()
    {
        var (client, handlerMock) = Build(HttpStatusCode.OK, WrapInGeminiEnvelope("{}"));

        await client.AnalyzeLogsAsync(SampleLogs(), 0);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.Content!.Headers.ContentType!.MediaType == "application/json"),
            ItExpr.IsAny<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AnalyzeLogsAsync_ServerReturns400_ThrowsHttpRequestException()
    {
        var (client, _) = Build(HttpStatusCode.BadRequest, "bad request");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.AnalyzeLogsAsync(SampleLogs(), 0));
    }

    [Fact]
    public async Task AnalyzeLogsAsync_ServerReturns500_ThrowsHttpRequestException()
    {
        var (client, _) = Build(HttpStatusCode.InternalServerError, "server error");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.AnalyzeLogsAsync(SampleLogs(), 0));
    }
}
