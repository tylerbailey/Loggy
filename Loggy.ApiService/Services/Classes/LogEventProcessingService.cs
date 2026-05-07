using Loggy.ApiService.Services.Interfaces;
using Loggy.Models;
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
            var properties = firstEvent.Schema.Keys.ToList();
            return properties;
        }

        public Dictionary<string, List<LogEvent>> GroupBy(List<LogEvent> events, string sortKey)
        {
            return events.GroupBy(e => e.Schema.ContainsKey(sortKey) ? e.Schema[sortKey] : "Undefined")
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

            // JSON array
            if (content.StartsWith('['))
            {
                var eventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(content, _jsonOptions);
                events.Add(new LogEvent { Schema = eventData?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()) ?? [] });
            }
            else
            {

                foreach (var line in content.Split('\n'))
                {

                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    var eventData = JsonSerializer.Deserialize<Dictionary<String, JsonElement>>(trimmed, _jsonOptions);
                    events.Add(new LogEvent { Schema = eventData?.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()) ?? [] });

                }
            }
            for (int i = 0; i < events.Count; i++)
                events[i].Id = i + 1;

            return events;
        }

      
        }
    }
