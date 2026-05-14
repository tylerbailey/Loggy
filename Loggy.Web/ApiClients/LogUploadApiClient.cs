using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Loggy.Web.ApiClients;

public class LogUploadApiClient(HttpClient httpClient)
{
    public async Task<string> UploadLogAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024, cancellationToken: cancellationToken); // 10MB limit, adjust as needed
        using var streamContent = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        content.Add(streamContent, "file", file.Name);
        var url = $"/api/LogEventProcessing/ProcessEvents";

        var response = await httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var eventsJson = await response.Content.ReadAsStringAsync(cancellationToken);

        return eventsJson ?? "";
    }

    public async Task<List<string>> GetSortKeys (List<LogEvent> logEvents, CancellationToken cancellationToken = default)
    {
        var url = $"/api/LogEventProcessing/GetSortKeys";
        var response = await httpClient.PostAsJsonAsync<List<LogEvent>>(url, logEvents, cancellationToken);
        response.EnsureSuccessStatusCode();
        var sortKeysJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var sortKeys = JsonSerializer.Deserialize<List<string>>(sortKeysJson);
        return sortKeys ?? [];
    }

    public async Task<Dictionary<string, List<LogEvent>>> GroupBy(List<LogEvent> events, string sortKey, CancellationToken cancellationToken = default)
    {
        var url = $"/api/LogEventProcessing/GroupBy?sortKey={sortKey}";
        var response = await httpClient.PostAsJsonAsync(url, events, cancellationToken);
        response.EnsureSuccessStatusCode();
        var groupedJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var groupedEvents = JsonSerializer.Deserialize<Dictionary<string, List<LogEvent>>>(groupedJson);
        return groupedEvents ?? [];
    }
}


