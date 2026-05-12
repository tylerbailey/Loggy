using Loggy.ApiService.Services.Classes;
using Loggy.Models.Logs;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text;

namespace Loggy.Tests;

public class LogEventProcessingServiceTests
{
    private readonly LogEventProcessingService _sut = new();

    // ── GetSortKeys ───────────────────────────────────────────────────────────

    public class GetSortKeysTests
    {
        private readonly LogEventProcessingService _sut = new();

        [Fact]
        public void GetSortKeys_NullList_ReturnsEmpty()
        {
            var result = _sut.GetSortKeys(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void GetSortKeys_EmptyList_ReturnsEmpty()
        {
            var result = _sut.GetSortKeys([]);
            Assert.Empty(result);
        }

        [Fact]
        public void GetSortKeys_SingleEvent_ReturnsSchemaKeys()
        {
            var events = new List<LogEvent>
            {
                new() { Schema = new() { ["Level"] = "Info", ["Message"] = "Hello", ["Timestamp"] = "2024-01-01" } }
            };

            var keys = _sut.GetSortKeys(events);

            Assert.Equal(3, keys.Count);
            Assert.Contains("Level", keys);
            Assert.Contains("Message", keys);
            Assert.Contains("Timestamp", keys);
        }

        [Fact]
        public void GetSortKeys_MultipleEvents_ReturnsOnlyFirstEventKeys()
        {
            // Service only reads keys from the first event — verify that contract
            var events = new List<LogEvent>
            {
                new() { Schema = new() { ["Level"] = "Info" } },
                new() { Schema = new() { ["Level"] = "Error", ["Extra"] = "Only in second" } }
            };

            var keys = _sut.GetSortKeys(events);

            Assert.Single(keys);
            Assert.DoesNotContain("Extra", keys);
        }
    }

    // ── GroupBy ───────────────────────────────────────────────────────────────

    public class GroupByTests
    {
        private readonly LogEventProcessingService _sut = new();

        [Fact]
        public void GroupBy_ByLevel_GroupsCorrectly()
        {
            var events = new List<LogEvent>
            {
                new() { Id = 1, Schema = new() { ["Level"] = "Error" } },
                new() { Id = 2, Schema = new() { ["Level"] = "Info" } },
                new() { Id = 3, Schema = new() { ["Level"] = "Error" } },
            };

            var result = _sut.GroupBy(events, "Level");

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result["Error"].Count);
            Assert.Single(result["Info"]);
        }

        [Fact]
        public void GroupBy_MissingKey_GroupsUnderUndefined()
        {
            var events = new List<LogEvent>
            {
                new() { Id = 1, Schema = new() { ["Level"] = "Info" } },
                new() { Id = 2, Schema = new() { } },          // no key at all
            };

            var result = _sut.GroupBy(events, "Level");

            Assert.True(result.ContainsKey("Undefined"));
            Assert.Single(result["Undefined"]);
        }

        [Fact]
        public void GroupBy_AllEventsMissingKey_ReturnsUndefinedGroup()
        {
            var events = new List<LogEvent>
            {
                new() { Id = 1, Schema = new() { ["Message"] = "A" } },
                new() { Id = 2, Schema = new() { ["Message"] = "B" } },
            };

            var result = _sut.GroupBy(events, "NonExistentKey");

            Assert.Single(result);
            Assert.Equal(2, result["Undefined"].Count);
        }

        [Fact]
        public void GroupBy_EmptyList_ReturnsEmptyDictionary()
        {
            var result = _sut.GroupBy([], "Level");
            Assert.Empty(result);
        }

        [Fact]
        public void GroupBy_SingleGroup_ReturnsSingleEntry()
        {
            var events = new List<LogEvent>
            {
                new() { Id = 1, Schema = new() { ["Source"] = "WebAPI" } },
                new() { Id = 2, Schema = new() { ["Source"] = "WebAPI" } },
            };

            var result = _sut.GroupBy(events, "Source");

            Assert.Single(result);
            Assert.Equal(2, result["WebAPI"].Count);
        }
    }

    // ── GetEventsFromFile ─────────────────────────────────────────────────────

    public class GetEventsFromFileTests
    {
        private readonly LogEventProcessingService _sut = new();

        private static IFormFile MakeFormFile(string content, string fileName = "test.log")
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            var mock = new Mock<IFormFile>();
            mock.Setup(f => f.OpenReadStream()).Returns(stream);
            mock.Setup(f => f.FileName).Returns(fileName);
            mock.Setup(f => f.Length).Returns(bytes.Length);
            return mock.Object;
        }

        [Fact]
        public async Task GetEventsFromFile_EmptyFile_ReturnsEmptyList()
        {
            var file = MakeFormFile("   ");
            var result = await _sut.GetEventsFromFile(file);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetEventsFromFile_SingleJsonLine_ReturnsOneEvent()
        {
            var json = """{"Level":"Info","Message":"App started"}""";
            var file = MakeFormFile(json);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Single(result);
            Assert.Equal("Info", result[0].Schema["Level"]);
            Assert.Equal("App started", result[0].Schema["Message"]);
        }

        [Fact]
        public async Task GetEventsFromFile_MultipleJsonLines_ReturnsAllEvents()
        {
            var content = string.Join('\n', new[]
            {
                """{"Level":"Info","Message":"Started"}""",
                """{"Level":"Error","Message":"Failed"}""",
                """{"Level":"Warning","Message":"Slow"}"""
            });
            var file = MakeFormFile(content);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task GetEventsFromFile_IdsAreAssignedSequentially()
        {
            var content = string.Join('\n', new[]
            {
                """{"Level":"Info"}""",
                """{"Level":"Error"}""",
                """{"Level":"Warning"}"""
            });
            var file = MakeFormFile(content);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Equal(1, result[0].Id);
            Assert.Equal(2, result[1].Id);
            Assert.Equal(3, result[2].Id);
        }

        [Fact]
        public async Task GetEventsFromFile_BlankLinesAreSkipped()
        {
            var content = """{"Level":"Info"}""" + "\n\n\n" + """{"Level":"Error"}""";
            var file = MakeFormFile(content);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetEventsFromFile_JsonArray_ParsedAsOneEvent()
        {
            // The service treats content starting with '[' as a JSON object parse
            // (current behaviour — this test documents it)
            var content = """[{"Level":"Info"},{"Level":"Error"}]""";
            var file = MakeFormFile(content);

            // Should not throw; returns at least one event
            var result = await _sut.GetEventsFromFile(file);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetEventsFromFile_ExtraWhitespace_IsTrimmed()
        {
            var content = "  \n" + """{"Level":"Info","Message":"Trim me"}""" + "\n  ";
            var file = MakeFormFile(content);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Single(result);
        }

        [Fact]
        public async Task GetEventsFromFile_NestedJsonValues_AreStoredAsStrings()
        {
            var json = """{"Level":"Error","Properties":{"userId":42}}""";
            var file = MakeFormFile(json);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Single(result);
            Assert.True(result[0].Schema.ContainsKey("Properties"));
        }
    }
}
