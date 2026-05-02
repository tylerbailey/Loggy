using System.Net.Http.Headers;
using System.Text.Json;
using Loggy.Models;
using Microsoft.AspNetCore.Components.Forms;
namespace Loggy.Web;

public class LogUploadApiClient(HttpClient httpClient)
{
   public async Task<Dictionary<string, List<LogEvent>>> UploadLogAsync(IBrowserFile file, CancellationToken cancellationToken = default)
{
    using var content = new MultipartFormDataContent();
    using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024); // 10MB limit, adjust as needed
    using var streamContent = new StreamContent(stream);
    
    streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
    content.Add(streamContent, "file", file.Name);

    var response = await httpClient.PostAsync("/api/Upload/uploadlog", content, cancellationToken);
    
    response.EnsureSuccessStatusCode();
    
    var json = await response.Content.ReadAsStringAsync(cancellationToken);
    return JsonSerializer.Deserialize<Dictionary<string, List<LogEvent>>>(json) ?? [];
}
}


