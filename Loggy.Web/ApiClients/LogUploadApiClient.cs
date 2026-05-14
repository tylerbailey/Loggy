using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Loggy.Web.ApiClients;

/// <summary>
/// Handles communication with the <c>LogEventProcessing</c> API endpoints.
/// Responsible for uploading log files, retrieving available group-by keys,
/// and fetching events grouped by a chosen field.
/// </summary>
public class LogUploadApiClient(HttpClient httpClient)
{
    /// <summary>
    /// Uploads a log file to the backend for parsing and returns the resulting
    /// events as a raw JSON string. The file is streamed as multipart/form-data
    /// to avoid loading the entire file into memory before sending.
    /// </summary>
    /// <param name="file">
    /// The browser-selected log file. Must not exceed 10 MB — larger files will
    /// throw an <see cref="IOException"/> from the Blazor stream.
    /// </param>
    /// <param name="cancellationToken">Token for cancelling the upload.</param>
    /// <returns>
    /// A JSON string representing a <c>List&lt;LogEvent&gt;</c>, or an empty
    /// string if the response body is null.
    /// </returns>
    public async Task<string> UploadLogAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();

        // Cap the readable stream at 10 MB. OpenReadStream enforces this limit and
        // will throw if the file is larger, preventing runaway memory usage in the browser.
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024, cancellationToken: cancellationToken);
        using var streamContent = new StreamContent(stream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        content.Add(streamContent, "file", file.Name);

        var url = "/api/LogEventProcessing/ProcessEvents";

        var response = await httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var eventsJson = await response.Content.ReadAsStringAsync(cancellationToken);

        return eventsJson ?? "";
    }

    /// <summary>
    /// Retrieves the list of field names (sort/group keys) available on the supplied
    /// log events. Keys are derived server-side from the first event's schema.
    /// </summary>
    /// <param name="logEvents">The parsed events whose schema fields should be inspected.</param>
    /// <param name="cancellationToken">Token for cancelling the request.</param>
    /// <returns>
    /// A list of field name strings (e.g. <c>["Level", "Message", "Timestamp"]</c>),
    /// or an empty list if the response could not be deserialized.
    /// </returns>
    public async Task<List<string>> GetSortKeys(List<LogEvent> logEvents, CancellationToken cancellationToken = default)
    {
        var url = "/api/LogEventProcessing/GetSortKeys";
        var response = await httpClient.PostAsJsonAsync<List<LogEvent>>(url, logEvents, cancellationToken);
        response.EnsureSuccessStatusCode();
        var sortKeysJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var sortKeys = JsonSerializer.Deserialize<List<string>>(sortKeysJson);
        return sortKeys ?? [];
    }

    /// <summary>
    /// Groups the supplied log events by the value of a specified schema field,
    /// delegating the grouping logic to the backend.
    /// </summary>
    /// <param name="events">The events to group.</param>
    /// <param name="sortKey">
    /// The schema field to group by (e.g. <c>"Level"</c>). Passed as a query
    /// parameter — events that lack this field are grouped under <c>"Undefined"</c>
    /// by the server.
    /// </param>
    /// <param name="cancellationToken">Token for cancelling the request.</param>
    /// <returns>
    /// A dictionary mapping each distinct field value to its list of matching events,
    /// or an empty dictionary if the response could not be deserialized.
    /// </returns>
    public async Task<Dictionary<string, List<LogEvent>>> GroupBy(List<LogEvent> events, string sortKey, CancellationToken cancellationToken = default)
    {
        // sortKey is appended as a query parameter because the controller binds
        // it with [FromQuery] rather than from the request body.
        var url = $"/api/LogEventProcessing/GroupBy?sortKey={sortKey}";
        var response = await httpClient.PostAsJsonAsync(url, events, cancellationToken);
        response.EnsureSuccessStatusCode();
        var groupedJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var groupedEvents = JsonSerializer.Deserialize<Dictionary<string, List<LogEvent>>>(groupedJson);
        return groupedEvents ?? [];
    }
}