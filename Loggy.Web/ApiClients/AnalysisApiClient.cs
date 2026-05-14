using Loggy.Models;
using Loggy.Models.Gemini;
using Loggy.Models.Logs.Classes;
using System.Text.Json;

namespace Loggy.Web.ApiClients
{
    /// <summary>
    /// Sends log events to the backend AI analysis endpoint and deserializes
    /// the structured <see cref="LogAnalysis"/> result. Supports multiple AI
    /// model backends (e.g. Gemini, Custom) selected at call time.
    /// </summary>
    public class AnalysisApiClient(HttpClient httpClient)
    {
        public async Task<LogAnalysis> AnalyzeLogsAsync(List<LogEvent> logs, int modelOption, CancellationToken cancellationToken = default)
        {
            var translatedModelOption = Enum.Parse<Enums.ModelOptions>(modelOption.ToString());
            var url = $"/api/{translatedModelOption}API/Query";

            var response = await httpClient.PostAsJsonAsync(url, logs, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            // Step 1: unwrap the Gemini envelope to get the raw text the model produced.
            var responseObjects = JsonSerializer.Deserialize<GeminiResponse>(responseBody) ?? new GeminiResponse();
            var text = responseObjects.Candidates.FirstOrDefault()?.Content.Parts.FirstOrDefault()?.Text ?? "No response";

            // Step 2: parse the model's text output as a LogAnalysis JSON object.
            // The model is prompted to return only JSON, but if it includes markdown
            // fences or other formatting the parse will fail — return an empty analysis
            // rather than propagating the exception to the UI.
            LogAnalysis? analysis = null;
            try
            {
                analysis = JsonSerializer.Deserialize<LogAnalysis>(text);
            }
            catch (JsonException)
            {
                // Handle JSON parsing error, possibly log the error or return a default LogAnalysis
            }

            return analysis ?? new LogAnalysis();
        }
    }
}