using Loggy.Models;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Loggy.Web;

public class LogUploadApiClient(HttpClient httpClient)
{
    public async Task<Dictionary<string, List<LogEvent>>> UploadLogAsync(IBrowserFile file, string schemaType, int sortOption, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB limit, adjust as needed
        using var streamContent = new StreamContent(stream);

        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        var url = sortOption switch
        {
            0 => $"/api/{schemaType}/SortByException",
            1 => $"/api/{schemaType}/SortByTimeStamp",
            _ => throw new ArgumentException("Unknown sort option")
        };


        var response = await httpClient.PostAsync(url, content, cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, List<LogEvent>>>(json) ?? [];
    }
}


