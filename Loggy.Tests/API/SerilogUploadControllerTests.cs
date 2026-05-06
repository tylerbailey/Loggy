using System.Text;
using System.Text.Json;
using Loggy.ApiService.Controllers.Classes;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Loggy.ApiService.Tests.Controllers;

public class SerilogUploadControllerTests
{
    private readonly Mock<IEventProcessorService> _serviceMock = new();
    private readonly SerilogUploadController _sut;

    public SerilogUploadControllerTests()
    {
        _sut = new SerilogUploadController(_serviceMock.Object);
    }

    // -------------------------------------------------------------------------
    // SortByException
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SortByException_NullFile_ReturnsBadRequest()
    {
        var result = await _sut.SortByException(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("File is required.", bad.Value);
    }

    [Fact]
    public async Task SortByException_ValidFile_ReturnsContentResultWithJson()
    {
        var file = MakeFormFile("irrelevant – service is mocked");
        var events = new List<SeriLogEvent>
        {
            new() { Exception = "IOException", Message = "disk full" },
            new() { Exception = "IOException", Message = "read error" },
        };
        var grouped = new Dictionary<string, List<SeriLogEvent>>
        {
            ["IOException"] = events
        };

        _serviceMock.Setup(s => s.GetEventsFromFile(file))
                    .ReturnsAsync(events);
        _serviceMock.Setup(s => s.SortEventsByException(events))
                    .Returns(grouped);

        var result = await _sut.SortByException(file);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, List<SeriLogEvent>>>(
            content.Content!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(deserialized);
        Assert.True(deserialized.ContainsKey("IOException"));
        Assert.Equal(2, deserialized["IOException"].Count);
    }

    [Fact]
    public async Task SortByException_CallsServiceMethodsInOrder()
    {
        var file = MakeFormFile("{}");
        var events = new List<SeriLogEvent>();

        _serviceMock.Setup(s => s.GetEventsFromFile(file)).ReturnsAsync(events);
        _serviceMock.Setup(s => s.SortEventsByException(events))
                    .Returns(new Dictionary<string, List<SeriLogEvent>>());

        await _sut.SortByException(file);

        _serviceMock.Verify(s => s.GetEventsFromFile(file), Times.Once);
        _serviceMock.Verify(s => s.SortEventsByException(events), Times.Once);
    }

    // -------------------------------------------------------------------------
    // SortByTimeStamp
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SortByTimeStamp_NullFile_ReturnsBadRequest()
    {
        var result = await _sut.SortByTimeStamp(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("File is required.", bad.Value);
    }

    [Fact]
    public async Task SortByTimeStamp_ValidFile_ReturnsContentResultWithJson()
    {
        var file = MakeFormFile("irrelevant");
        var ts = new DateTime(2024, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var events = new List<SeriLogEvent> { new() { Timestamp = ts, Message = "startup" } };
        var grouped = new Dictionary<string, List<SeriLogEvent>>
        {
            [ts.ToString()] = events
        };

        _serviceMock.Setup(s => s.GetEventsFromFile(file)).ReturnsAsync(events);
        _serviceMock.Setup(s => s.SortEventsByTimeStamp(events)).Returns(grouped);

        var result = await _sut.SortByTimeStamp(file);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.NotNull(content.Content);
    }

    [Fact]
    public async Task SortByTimeStamp_CallsServiceMethodsInOrder()
    {
        var file = MakeFormFile("{}");
        var events = new List<SeriLogEvent>();

        _serviceMock.Setup(s => s.GetEventsFromFile(file)).ReturnsAsync(events);
        _serviceMock.Setup(s => s.SortEventsByTimeStamp(events))
                    .Returns(new Dictionary<string, List<SeriLogEvent>>());

        await _sut.SortByTimeStamp(file);

        _serviceMock.Verify(s => s.GetEventsFromFile(file), Times.Once);
        _serviceMock.Verify(s => s.SortEventsByTimeStamp(events), Times.Once);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IFormFile MakeFormFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(bytes));
        mock.Setup(f => f.FileName).Returns("log.json");
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }
}
