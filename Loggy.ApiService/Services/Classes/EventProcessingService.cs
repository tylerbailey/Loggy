using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Loggy.Models;
using Loggy.ApiService.Services.Interfaces;

namespace Loggy.ApiService.Services.Classes
{
    public class EventProcessorService : IEventProcessorService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Dictionary<string, List<LogEvent>> SortEventsByException(List<LogEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);

            return events
                .GroupBy(e => e?.Exception)
                .ToDictionary(g => g.Key ?? "<null>", g => g.ToList());
        }

        public async Task<List<LogEvent>> GetEventsFromFile(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            string content = await reader.ReadToEndAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(content))
                return [];

            content = content.Trim();

            // JSON array
            if (content.StartsWith('['))
            {
                return JsonSerializer.Deserialize<List<LogEvent>>(content, _jsonOptions) ?? [];
            }

            // NDJSON - one JSON object per line
            var events = new List<LogEvent>();
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                var logEvent = JsonSerializer.Deserialize<LogEvent>(trimmed, _jsonOptions);
                if (logEvent != null)
                    events.Add(logEvent);
            }
            return events;
        }

        public Dictionary<string, List<LogEvent>> SortEventsByTimeStamp(List<LogEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);

            return events
                .GroupBy(e => e?.Timestamp)
                .ToDictionary(g => g.Key.ToString() ?? "No timestamp", g => g.ToList());
        }
    }
}