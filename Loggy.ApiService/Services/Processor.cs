using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Loggy.ApiService.Models;

namespace Loggy.ApiService.Services
{
    public class Processor
    {
        public Dictionary<string, List<LogEvent>> SortEventsByException(List<LogEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);

            // Group by Exception (elements or Exception may be null). Normalize null keys to a non-null placeholder.
            return events
                .GroupBy(e => e?.Exception)
                .ToDictionary(g => g.Key ?? "<null>", g => g.ToList());
        }

        public async Task<List<LogEvent>> GetEventsFromFile(IFormFile file)
        {
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string content = await reader.ReadToEndAsync().ConfigureAwait(false);

                List<LogEvent>? events = null;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    events = JsonSerializer.Deserialize<List<LogEvent>>(content);
                }

                events ??= new List<LogEvent>();
                return events;
            }
        }
    }
}
