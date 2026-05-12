using Loggy.ApiService.Controllers.Classes;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models.Logs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Loggy.Tests;

public class LogEventProcessingControllerTests
{
    private readonly Mock<IEventProcessingService> _serviceMock = new();
    private readonly LogEventProcessingController _sut;

    public LogEventProcessingControllerTests()
    {
        _sut = new LogEventProcessingController(_serviceMock.Object);
    }

    // ── ProcessEventsFromFile ─────────────────────────────────────────────────

    [Fact]
    public async Task ProcessEventsFromFile_NullFile_ReturnsBadRequest()
    {
        var result = await _sut.ProcessEventsFromFile(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("required", bad.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessEventsFromFile_ValidFile_ReturnsJsonContent()
    {
        var fileMock = new Mock<IFormFile>();
        var events = new List<LogEvent>
        {
            new() { Id = 1, Schema = new() { ["Level"] = "Info" } }
        };
        _serviceMock.Setup(s => s.GetEventsFromFile(fileMock.Object)).ReturnsAsync(events);

        var result = await _sut.ProcessEventsFromFile(fileMock.Object);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Contains("\"Id\":1", content.Content);
    }

    [Fact]
    public async Task ProcessEventsFromFile_EmptyFile_ReturnsEmptyJsonArray()
    {
        var fileMock = new Mock<IFormFile>();
        _serviceMock.Setup(s => s.GetEventsFromFile(fileMock.Object)).ReturnsAsync([]);

        var result = await _sut.ProcessEventsFromFile(fileMock.Object);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("[]", content.Content);
    }

    // ── GetSortKeys ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSortKeys_NullList_ReturnsBadRequest()
    {
        var result = await _sut.GetSortKeys(null!);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSortKeys_EmptyList_ReturnsBadRequest()
    {
        var result = await _sut.GetSortKeys([]);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetSortKeys_ValidEvents_ReturnsJsonKeys()
    {
        var events = new List<LogEvent>
        {
            new() { Id = 1, Schema = new() { ["Level"] = "Info", ["Message"] = "Hi" } }
        };
        _serviceMock.Setup(s => s.GetSortKeys(events)).Returns(["Level", "Message"]);

        var result = await _sut.GetSortKeys(events);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Contains("Level", content.Content);
        Assert.Contains("Message", content.Content);
    }

    // ── GroupBy ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GroupBy_NullList_ReturnsBadRequest()
    {
        var result = await _sut.GroupBy(null!, "Level");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GroupBy_EmptyList_ReturnsBadRequest()
    {
        var result = await _sut.GroupBy([], "Level");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GroupBy_ValidEvents_ReturnsGroupedJson()
    {
        var events = new List<LogEvent>
        {
            new() { Id = 1, Schema = new() { ["Level"] = "Error" } },
            new() { Id = 2, Schema = new() { ["Level"] = "Info" } },
        };
        var grouped = new Dictionary<string, List<LogEvent>>
        {
            ["Error"] = [events[0]],
            ["Info"]  = [events[1]],
        };
        _serviceMock.Setup(s => s.GroupBy(events, "Level")).Returns(grouped);

        var result = await _sut.GroupBy(events, "Level");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", content.ContentType);
        Assert.Contains("Error", content.Content);
        Assert.Contains("Info", content.Content);
    }

    [Fact]
    public async Task GroupBy_CallsServiceWithCorrectKey()
    {
        var events = new List<LogEvent> { new() { Id = 1, Schema = new() { ["Source"] = "API" } } };
        _serviceMock.Setup(s => s.GroupBy(events, "Source"))
                    .Returns(new Dictionary<string, List<LogEvent>> { ["API"] = [events[0]] });

        await _sut.GroupBy(events, "Source");

        _serviceMock.Verify(s => s.GroupBy(events, "Source"), Times.Once);
    }
}
