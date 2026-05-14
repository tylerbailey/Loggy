using Loggy.ApiService.Services.Classes;
using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text;

namespace Loggy.Tests;

public class LogEventProcessingServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a LogEvent whose Log tree mirrors a flat JSON object:
    ///   Root -> TreeNode("key") -> TreeNode("value")
    /// </summary>
    internal static LogEvent MakeEvent(int id, params (string key, string value)[] fields)
    {
        var root = new TreeNode<string>("Root");
        foreach (var (key, value) in fields)
        {
            var keyNode = new TreeNode<string>(key);
            keyNode.AddChild(new TreeNode<string>(value));
            root.AddChild(keyNode);
        }
        return new LogEvent { Id = id, Log = root };
    }

    internal static IFormFile MakeFormFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.FileName).Returns("test.log");
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }

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
        public void GetSortKeys_SingleEvent_ReturnsTopLevelKeys()
        {
            var events = new List<LogEvent>
            {
                MakeEvent(1, ("Level", "Info"), ("Message", "Hello"), ("Timestamp", "2024-01-01"))
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
            var events = new List<LogEvent>
            {
                MakeEvent(1, ("Level", "Info")),
                MakeEvent(2, ("Level", "Error"), ("Extra", "Only in second"))
            };

            var keys = _sut.GetSortKeys(events);

            Assert.Single(keys);
            Assert.DoesNotContain("Extra", keys);
        }

        [Fact]
        public void GetSortKeys_EventWithNoChildren_ReturnsEmpty()
        {
            var emptyEvent = new LogEvent { Id = 1, Log = new TreeNode<string>("Root") };
            var result = _sut.GetSortKeys([emptyEvent]);
            Assert.Empty(result);
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
                MakeEvent(1, ("Level", "Error")),
                MakeEvent(2, ("Level", "Info")),
                MakeEvent(3, ("Level", "Error")),
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
                MakeEvent(1, ("Level", "Info")),
                MakeEvent(2, ("Message", "No level here")),
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
                MakeEvent(1, ("Message", "A")),
                MakeEvent(2, ("Message", "B")),
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
                MakeEvent(1, ("Source", "WebAPI")),
                MakeEvent(2, ("Source", "WebAPI")),
            };

            var result = _sut.GroupBy(events, "Source");

            Assert.Single(result);
            Assert.Equal(2, result["WebAPI"].Count);
        }

        [Fact]
        public void GroupBy_PreservesEventIds()
        {
            var events = new List<LogEvent>
            {
                MakeEvent(42, ("Level", "Error")),
                MakeEvent(99, ("Level", "Error")),
            };

            var result = _sut.GroupBy(events, "Level");

            var ids = result["Error"].Select(e => e.Id).ToList();
            Assert.Contains(42, ids);
            Assert.Contains(99, ids);
        }

        [Fact]
        public void GroupBy_NullLog_GroupsUnderUndefined()
        {
            var events = new List<LogEvent>
            {
                new() { Id = 1, Log = null! },
                MakeEvent(2, ("Level", "Info")),
            };

            var result = _sut.GroupBy(events, "Level");

            Assert.True(result.ContainsKey("Undefined"));
            Assert.Single(result["Undefined"]);
        }
    }

    // ── GetEventsFromFile ─────────────────────────────────────────────────────

    public class GetEventsFromFileTests
    {
        private readonly LogEventProcessingService _sut = new();

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
        }

        [Fact]
        public async Task GetEventsFromFile_SingleJsonLine_TreeHasCorrectKeys()
        {
            var json = """{"Level":"Info","Message":"App started"}""";
            var file = MakeFormFile(json);

            var result = await _sut.GetEventsFromFile(file);
            var keyNodes = result[0].Log.Children.Select(c => c.Value).ToList();

            Assert.Contains("Level", keyNodes);
            Assert.Contains("Message", keyNodes);
        }

        [Fact]
        public async Task GetEventsFromFile_SingleJsonLine_TreeHasCorrectValues()
        {
            var json = """{"Level":"Info","Message":"App started"}""";
            var file = MakeFormFile(json);

            var result = await _sut.GetEventsFromFile(file);
            var levelNode = result[0].Log.Children.First(c => c.Value == "Level");

            Assert.Equal("Info", levelNode.Children.First().Value);
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
        public async Task GetEventsFromFile_NestedJsonObject_CreatesChildNodes()
        {
            var json = """{"Level":"Error","Properties":{"userId":"42","host":"server1"}}""";
            var file = MakeFormFile(json);

            var result = await _sut.GetEventsFromFile(file);

            Assert.Single(result);
            var propsNode = result[0].Log.Children.FirstOrDefault(c => c.Value == "Properties");
            Assert.NotNull(propsNode);
            Assert.NotEmpty(propsNode.Children);
        }

        [Fact]
        public async Task GetEventsFromFile_EachEventHasItsOwnRootNode()
        {
            var content = string.Join('\n', new[]
            {
                """{"Level":"Info"}""",
                """{"Level":"Error"}"""
            });
            var file = MakeFormFile(content);

            var result = await _sut.GetEventsFromFile(file);

            Assert.NotSame(result[0].Log, result[1].Log);
        }
    }

    // ── TreeNode ──────────────────────────────────────────────────────────────

    public class TreeNodeTests
    {
        [Fact]
        public void IsLeaf_NoChildren_ReturnsTrue()
        {
            var node = new TreeNode<string>("value");
            Assert.True(node.IsLeaf());
        }

        [Fact]
        public void IsLeaf_WithChildren_ReturnsFalse()
        {
            var node = new TreeNode<string>("parent");
            node.AddChild(new TreeNode<string>("child"));
            Assert.False(node.IsLeaf());
        }

        [Fact]
        public void IsRoot_NoParent_ReturnsTrue()
        {
            var node = new TreeNode<string>("root");
            Assert.True(node.IsRoot());
        }

        [Fact]
        public void IsRoot_WithParent_ReturnsFalse()
        {
            var parent = new TreeNode<string>("parent");
            var child = new TreeNode<string>("child");
            parent.AddChild(child);
            Assert.False(child.IsRoot());
        }

        [Fact]
        public void AddChild_SetsParentReference()
        {
            var parent = new TreeNode<string>("parent");
            var child = new TreeNode<string>("child");
            parent.AddChild(child);
            Assert.Same(parent, child.Parent);
        }

        [Fact]
        public void AddChild_AppearsInChildrenList()
        {
            var parent = new TreeNode<string>("parent");
            var child = new TreeNode<string>("child");
            parent.AddChild(child);
            Assert.Contains(child, parent.Children);
        }

        [Fact]
        public void AddChild_MultipleChildren_AllPresent()
        {
            var parent = new TreeNode<string>("parent");
            parent.AddChild(new TreeNode<string>("a"));
            parent.AddChild(new TreeNode<string>("b"));
            parent.AddChild(new TreeNode<string>("c"));
            Assert.Equal(3, parent.Children.Count);
        }
    }
}