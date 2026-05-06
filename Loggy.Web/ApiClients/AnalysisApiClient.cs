using Loggy.Models;
using Loggy.Models.Gemini;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loggy.Web.ApiClients
{
    public class AnalysisApiClient(HttpClient httpClient)
    {

        public async Task<LogAnalysis> AnalyzeLogsAsync(Dictionary<string, List<SeriLogEvent>> logs, int modelOption, CancellationToken cancellationToken = default)
        {


            var translatedModelOption = Enum.Parse<Enums.ModelOptions>(modelOption.ToString());
            var url = $"/api/{translatedModelOption}API/Query";

            var response = await httpClient.PostAsJsonAsync(url, logs, cancellationToken);


            response.EnsureSuccessStatusCode();      
            var responseBody = await response.Content.ReadAsStringAsync();
            var responseObjects = JsonSerializer.Deserialize<GeminiResponse>(responseBody) ?? new GeminiResponse();
            var text = responseObjects.Candidates
    .FirstOrDefault()?.Content
    .Parts.FirstOrDefault()?.Text ?? "No response";

            var analysis = JsonSerializer.Deserialize<LogAnalysis>(text) ?? new LogAnalysis();
            return analysis;

        }
    }
}
