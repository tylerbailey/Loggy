using Loggy.Models;
using Microsoft.AspNetCore.Components.Forms;
using System.Data;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Loggy.Web.ApiClients;

public class LogUploadApiClient(HttpClient httpClient)
{
    public async Task<Dictionary<string, List<LogEvent>>> UploadLogAsync(IBrowserFile file, int schemaType, int sortOption, int modelOption, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024, cancellationToken: cancellationToken); // 10MB limit, adjust as needed
        using var streamContent = new StreamContent(stream);

        //Parse enum values
        var translatedSchemaType = Enum.Parse<Enums.SchemaTypes>(schemaType.ToString());
        var translatedSortOption = Enum.Parse<Enums.SortOptions>(sortOption.ToString());



        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        var url = $"/api/{translatedSchemaType}Upload/Sort{translatedSortOption}";


        var response = await httpClient.PostAsync(url, content, cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, List<LogEvent>>>(json) ?? [];
    }
}


