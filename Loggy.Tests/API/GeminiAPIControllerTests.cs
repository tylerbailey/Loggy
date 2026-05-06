using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Loggy.ApiService.Controllers.Classes;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Loggy.ApiService.Tests.Controllers;

/// <summary>
/// Tests for GeminiAPIController.QueryAsync.
///
/// The controller creates its own HttpClient via IHttpClientFactory (not the
/// named "gemini" client — it calls CreateClient() with no name). We intercept
/// the outbound HTTP call by supplying a mock handler via the factory so no
/// real network traffic is made.
/// </summary>
public class GeminiAPIControllerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (GeminiAPIController controller, Mock<HttpMessageHandler> handlerMock)
        BuildSut(HttpStatusCode statusCode, string responseBody)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var optionsMock = new Mock<IOptions<Options>>();
        optionsMock.Setup(o => o.Value).Returns(new Options { ApiKey = "test-key" });

        var serviceMock = new Mock<IEventProcessorService>();

        var controller = new GeminiAPIController(optionsMock.Object, factoryMock.Object, serviceMock.Object);
        return (controller, handlerMock);
    }

    private static Dictionary<string, List<SeriLogEvent>> SampleLogs() =>
        new()
        {
            ["app"] = new List<SeriLogEvent>
            {
                new()
                {
                    Id = 1,
                    Timestamp = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                    Level = "Error",
                    Message = "Something went wrong",
                    Exception = "NullReferenceException",
                    Source = "MyService"
                }
            }
        };

    // -------------------------------------------------------------------------
    // Happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_GeminiReturnsSuccess_ReturnsOkWithBody()
    {
        var geminiResponse = """{"candidates":[{"content":{"parts":[{"text":"{}"}]}}]}""";
        var (sut, _) = BuildSut(HttpStatusCode.OK, geminiResponse);

        var result = await sut.QueryAsync(SampleLogs());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(geminiResponse, ok.Value as string);
    }

    [Fact]
    public async Task QueryAsync_SendsRequestToGeminiEndpoint()
    {
        var (sut, handlerMock) = BuildSut(HttpStatusCode.OK, "{}");

        await sut.QueryAsync(SampleLogs());

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.Host == "generativelanguage.googleapis.com"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_RequestBodyContainsApiKey()
    {
        var (sut, handlerMock) = BuildSut(HttpStatusCode.OK, "{}");

        await sut.QueryAsync(SampleLogs());

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.Query.Contains("key=test-key")),
            ItExpr.IsAny<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Error path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_GeminiReturnsError_ReturnsBadRequest()
    {
        var errorBody = """{"error":{"message":"API key invalid"}}""";
        var (sut, _) = BuildSut(HttpStatusCode.BadRequest, errorBody);

        var result = await sut.QueryAsync(SampleLogs());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var msg = bad.Value as string;
        Assert.NotNull(msg);
        Assert.Contains("Gemini error:", msg);
        Assert.Contains("API key invalid", msg);
    }

    [Fact]
    public async Task QueryAsync_GeminiReturns429_ReturnsBadRequest()
    {
        var (sut, _) = BuildSut(HttpStatusCode.TooManyRequests, "rate limited");

        var result = await sut.QueryAsync(SampleLogs());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task QueryAsync_GeminiReturnsInternalServerError_ReturnsBadRequest()
    {
        var (sut, _) = BuildSut(HttpStatusCode.InternalServerError, "upstream error");

        var result = await sut.QueryAsync(SampleLogs());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // -------------------------------------------------------------------------
    // Edge cases for input serialisation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_EmptyLogDictionary_StillCallsGemini()
    {
        var (sut, handlerMock) = BuildSut(HttpStatusCode.OK, "{}");

        await sut.QueryAsync(new Dictionary<string, List<SeriLogEvent>>());

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_LogsWithSpecialCharacters_DoesNotThrow()
    {
        var logs = new Dictionary<string, List<SeriLogEvent>>
        {
            ["svc"] = new List<SeriLogEvent>
            {
                new() { Message = "User \"Alice\" logged <in> & out", Exception = null }
            }
        };

        var (sut, _) = BuildSut(HttpStatusCode.OK, "{}");

        // Should not throw when serialising special chars with UnsafeRelaxedJsonEscaping
        var ex = await Record.ExceptionAsync(() => sut.QueryAsync(logs));
        Assert.Null(ex);
    }
}
