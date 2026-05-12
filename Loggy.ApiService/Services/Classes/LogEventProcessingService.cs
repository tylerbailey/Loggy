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
            //var properties = firstEvent.Schema.Keys.ToList();
            return [];
        }

        public Dictionary<string, List<LogEvent>> GroupBy(List<LogEvent> events, string sortKey)
        {
            //return events.GroupBy(e => e.Schema.ContainsKey(sortKey) ? e.Schema[sortKey] : "Undefined")
            //             .ToDictionary(g => g.Key, g => g.ToList());
            return [];
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
                var eventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(content, _jsonOptions) ?? [];
                foreach (var item in eventData.Values)
                {                   
                    foreach (var key in eventData.Keys)
                    {
                        root.AddChild(ParseJsonElement(eventData[key]));
                    }
                }
            }
            else
            {
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    var trimmedEventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(trimmed, _jsonOptions) ?? [];
                    foreach (var key in trimmedEventData.Keys)
                    {
                        root.AddChild(ParseJsonElement(trimmedEventData[key]));
                    }
                }
            }
            //for (int i = 0; i < events.Count; i++)
            //    events[i].Id = i + 1;

            return events;
        }

        private TreeNode<string> ParseJsonElement(JsonElement element)
        {
            var eventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(element, _jsonOptions) ?? [];
            foreach (var key in eventData.Keys)
            {
                var parentNode = new TreeNode<string>(key);
                var item = eventData[key];
                if (item.ValueKind == JsonValueKind.Object)
                {
                    parentNode.Children.Add(ParseJsonElement(item));

                }
                else
                {
                    var value = item.GetRawText();
                    parentNode.Children.Add(new TreeNode<string>(value));
                }
                return parentNode;
            }
            return new TreeNode<string>("null");
        }

    }
}
