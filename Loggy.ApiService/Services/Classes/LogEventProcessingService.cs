using Loggy.ApiService.Services.Interfaces;
using Loggy.Models.Logs.Classes;
using System.Text.Json;

namespace Loggy.ApiService.Services.Classes
{
    /// <summary>
    /// Parses raw log files into <see cref="LogEvent"/> objects and provides
    /// utilities for inspecting and grouping them. Supports both JSON array
    /// format and newline-delimited JSON (NDJSON).
    /// </summary>
    public class LogEventProcessingService : IEventProcessingService
    {
        /// <summary>
        /// Shared deserialization options. Case-insensitive matching ensures log
        /// fields like "level", "Level", and "LEVEL" all bind correctly regardless
        /// of the source application's casing convention.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Returns the field names available on the log events, taken from the
        /// schema of the first event. These keys are used by the client to populate
        /// the group-by dropdown.
        /// </summary>
        /// <remarks>
        /// Only the first event's keys are inspected. If events have heterogeneous
        /// schemas, fields that appear only in later events will not be returned.
        /// </remarks>
        /// <param name="events">The parsed log events.</param>
        /// <returns>A list of field name strings, or an empty list if <paramref name="events"/> is null or empty.</returns>
        public List<string> GetSortKeys(List<LogEvent> events)
        {
            if (events == null || events.Count == 0)
                return [];
            var firstEvent = events[0];
            var keys = firstEvent.Log.Children.Select(c => c.Value).ToList();
            return keys;
        }

        /// <summary>
        /// Partitions the log events into groups based on the value of a specified
        /// schema field, returning a dictionary keyed by each distinct value.
        /// </summary>
        /// <param name="events">The events to group.</param>
        /// <param name="sortKey">The schema field to group by (e.g. <c>"Level"</c>).</param>
        /// <returns>
        /// A dictionary mapping each distinct field value to its matching events.
        /// Events where the field is absent are grouped under the key <c>"Undefined"</c>.
        /// </returns>
        public Dictionary<string, List<LogEvent>> GroupBy(List<LogEvent> events, string sortKey)
        {
            return events
                    .GroupBy(e => e.Log?.Children
                    .FirstOrDefault(c => c.Value == sortKey)?.Children.FirstOrDefault() ?.Value ?? "Undefined")
                    .ToDictionary(g => g.Key, g => g.ToList());

        }

        /// <summary>
        /// Reads a log file and parses its contents into a list of <see cref="LogEvent"/> objects.
        /// Supports two formats:
        /// <list type="bullet">
        ///   <item><description><b>JSON array</b> — a single <c>[...]</c> containing one object per event.</description></item>
        ///   <item><description><b>NDJSON</b> — one JSON object per line (newline-delimited JSON).</description></item>
        /// </list>
        /// Each returned event is assigned a sequential <c>Id</c> starting at 1.
        /// All field values are stored as strings in the event's <c>Schema</c> dictionary,
        /// preserving nested objects as their raw JSON representation.
        /// </summary>
        /// <param name="file">The uploaded log file.</param>
        /// <returns>
        /// A list of parsed <see cref="LogEvent"/> objects, or an empty list if the file is blank.
        /// </returns>
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

            // Assign stable 1-based IDs so downstream consumers (e.g. the Gemini
            // prompt) can reference specific events by Id in their output.
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
