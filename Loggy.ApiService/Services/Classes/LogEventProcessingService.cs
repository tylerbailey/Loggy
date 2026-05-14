using Loggy.ApiService.Services.Interfaces;
using Loggy.Models.Logs.Classes;
using System.Text.Json;

namespace Loggy.ApiService.Services.Classes
{
    public class LogEventProcessingService : IEventProcessingService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public List<string> GetSortKeys(List<LogEvent> events)
        {
            if (events == null || events.Count == 0)
                return [];
            var firstEvent = events[0];
            var keys = firstEvent.Log.Children.Select(c => c.Value).ToList();
            return keys;
        }

        public Dictionary<string, List<LogEvent>> GroupBy(List<LogEvent> events, string sortKey)
        {
            return events
                    .GroupBy(e => e.Log?.Children
                    .FirstOrDefault(c => c.Value == sortKey)?.Children.FirstOrDefault() ?.Value ?? "Undefined")
                    .ToDictionary(g => g.Key, g => g.ToList());

        }

        public async Task<List<LogEvent>> GetEventsFromFile(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            string content = await reader.ReadToEndAsync().ConfigureAwait(false);
            var events = new List<LogEvent>();
            if (string.IsNullOrWhiteSpace(content))
                return [];

            content = content.Trim();
            var root = new TreeNode<string>("Root");
            // JSON array
            if (content.StartsWith('['))
            {
                //top level
                var eventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(content, _jsonOptions) ?? [];
                foreach (var item in eventData.Values)
                {
                    foreach (var key in eventData.Keys)
                    {
                        var parent = new TreeNode<string>(key);
                        parent.Children.AddRange(ParseInnerJson(eventData[key]));
                        root.AddChild(parent);
                    }
                }
            }
            else
            {
                //new line delimited JSON
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    var trimmedEventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(trimmed, _jsonOptions) ?? [];
                    foreach (var key in trimmedEventData.Keys)
                    {
                        var parent = new TreeNode<string>(key);
                        parent.Children.AddRange(ParseInnerJson(trimmedEventData[key]));
                        root.AddChild(parent);
                    }
                    events.Add(new LogEvent() { Log = root });
                    root = new TreeNode<string>("Root");
                }
            }
            for (int i = 0; i < events.Count; i++)
                events[i].Id = i + 1;

            return events;
        }

        private List<TreeNode<string>> ParseInnerJson(JsonElement element)
        {
            var nodes = new List<TreeNode<string>>();
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var node = new TreeNode<string>(property.Name);
                    node.Children = ParseInnerJson(property.Value);
                    nodes.Add(node);
                }
            }
            else
                nodes.Add(new TreeNode<string>(element.ToString()));


            return nodes;
        }

    }
}
