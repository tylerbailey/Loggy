using System.Text.Json;
namespace Loggy.Models;

public sealed class LogEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string? Source { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, JsonElement> Properties { get; set; } = new();
}
