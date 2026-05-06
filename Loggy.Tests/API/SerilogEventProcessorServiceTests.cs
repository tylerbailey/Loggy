using System.Text;
using Loggy.ApiService.Services.Classes;
using Loggy.Models;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Loggy.ApiService.Tests.Services;

public class SerilogEventProcessorServiceTests
{
    private readonly SerilogEventProcessorService _sut = new();

    // -------------------------------------------------------------------------
    // GetEventsFromFile
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetEventsFromFile_JsonArray_ParsesAllEvents()
    {
        var json = """
            [
              {"timestamp":"2024-01-01T10:00:00Z","level":"Information","message":"App started","exception":null},
              {"timestamp":"2024-01-01T10:01:00Z","level":"Error","message":"Unhandled exception","exception":"NullReferenceException"}
            ]
            """;

        var file = MakeFormFile(json);
        var result = await _sut.GetEventsFromFile(file);

        Assert.Equal(2, result.Count);
        Assert.Equal("Information", result[0].Level);
        Assert.Equal("Error", result[1].Level);
        Assert.Equal("NullReferenceException", result[1].Exception);
    }

    [Fact]
    public async Task GetEventsFromFile_Ndjson_ParsesAllLines()
    {
        var ndjson =
            "{\"timestamp\":\"2024-01-01T10:00:00Z\",\"level\":\"Warning\",\"message\":\"Low disk\"}\n" +
            "{\"timestamp\":\"2024-01-01T10:01:00Z\",\"level\":\"Error\",\"message\":\"Crash\",\"exception\":\"IOException\"}";

        var file = MakeFormFile(ndjson);
        var result = await _sut.GetEventsFromFile(file);

        Assert.Equal(2, result.Count);
        Assert.Equal("Warning", result[0].Level);
        Assert.Equal("Error", result[1].Level);
    }

    [Fact]
    public async Task GetEventsFromFile_Ndjson_AssignsSequentialIds()
    {
        var ndjson =
            "{\"level\":\"Info\",\"message\":\"a\"}\n" +
            "{\"level\":\"Info\",\"message\":\"b\"}\n" +
            "{\"level\":\"Info\",\"message\":\"c\"}";

        var file = MakeFormFile(ndjson);
        var result = await _sut.GetEventsFromFile(file);

        Assert.Equal(new[] { 1, 2, 3 }, result.Select(e => e.Id));
    }

    [Fact]
    public async Task GetEventsFromFile_EmptyFile_ReturnsEmptyList()
    {
        var file = MakeFormFile("   ");
        var result = await _sut.GetEventsFromFile(file);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEventsFromFile_NdjsonWithBlankLines_SkipsBlankLines()
    {
        var ndjson = "{\"level\":\"Info\",\"message\":\"first\"}\n\n{\"level\":\"Info\",\"message\":\"second\"}";

        var file = MakeFormFile(ndjson);
        var result = await _sut.GetEventsFromFile(file);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEventsFromFile_JsonArray_IsCaseInsensitive()
    {
        // Serilog CLEF uses PascalCase field names
        var json = """[{"Timestamp":"2024-01-01T00:00:00Z","Level":"Error","Message":"oops"}]""";

        var file = MakeFormFile(json);
        var result = await _sut.GetEventsFromFile(file);

        Assert.Single(result);
        Assert.Equal("Error", result[0].Level);
        Assert.Equal("oops", result[0].Message);
    }

    // -------------------------------------------------------------------------
    // SortEventsByException
    // -------------------------------------------------------------------------

    [Fact]
    public void SortEventsByException_GroupsByExceptionType()
    {
        var events = new List<SeriLogEvent>
        {
            new() { Exception = "NullReferenceException", Message = "a" },
            new() { Exception = "IOException",            Message = "b" },
            new() { Exception = "NullReferenceException", Message = "c" },
            new() { Exception = null,                     Message = "d" },
        };

        var result = _sut.SortEventsByException(events);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result["NullReferenceException"].Count);
        Assert.Single(result["IOException"]);
        Assert.Single(result["None"]);
    }

    [Fact]
    public void SortEventsByException_AllEventsWithNoException_GroupedUnderNone()
    {
        var events = new List<SeriLogEvent>
        {
            new() { Exception = null, Message = "info1" },
            new() { Exception = null, Message = "info2" },
        };

        var result = _sut.SortEventsByException(events);

        Assert.Single(result);
        Assert.True(result.ContainsKey("None"));
        Assert.Equal(2, result["None"].Count);
    }

    [Fact]
    public void SortEventsByException_EmptyList_ReturnsEmptyDictionary()
    {
        var result = _sut.SortEventsByException(new List<SeriLogEvent>());
        Assert.Empty(result);
    }

    [Fact]
    public void SortEventsByException_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.SortEventsByException(null!));
    }

    // -------------------------------------------------------------------------
    // SortEventsByTimeStamp
    // -------------------------------------------------------------------------

    [Fact]
    public void SortEventsByTimeStamp_GroupsByTimestamp()
    {
        var t1 = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2024, 1, 1, 10, 1, 0, TimeSpan.Zero);

        var events = new List<SeriLogEvent>
    {
        new() { Timestamp = t1, Message = "a" },
        new() { Timestamp = t1, Message = "b" },
        new() { Timestamp = t2, Message = "c" },
    };

        var result = _sut.SortEventsByTimeStamp(events);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[t1.ToString()].Count);
        Assert.Single(result[t2.ToString()]);
    }

    [Fact]
    public void SortEventsByTimeStamp_EmptyList_ReturnsEmptyDictionary()
    {
        var result = _sut.SortEventsByTimeStamp(new List<SeriLogEvent>());
        Assert.Empty(result);
    }

    [Fact]
    public void SortEventsByTimeStamp_NullArgument_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.SortEventsByTimeStamp(null!));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IFormFile MakeFormFile(string content, string fileName = "log.json")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }
}
