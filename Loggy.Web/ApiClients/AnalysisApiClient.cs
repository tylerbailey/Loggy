using Loggy.Models;
using Loggy.Models.Gemini;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loggy.Web.ApiClients
{
    /// <summary>
    /// Sends log events to the backend AI analysis endpoint and deserializes
    /// the structured <see cref="LogAnalysis"/> result. Supports multiple AI
    /// model backends (e.g. Gemini, Custom) selected at call time.
    /// </summary>
    public class AnalysisApiClient(HttpClient httpClient)
    {
        /// <summary>
        /// Submits a list of log events to the selected AI model's Query endpoint
        /// and returns a structured analysis containing a summary, detected patterns,
        /// and aggregate error counts.
        /// </summary>
        /// <remarks>
        /// The response goes through two deserialization steps:
        /// <list type="number">
        ///   <item><description>
        ///     The raw HTTP response is deserialized as a <see cref="GeminiResponse"/>,
        ///     which wraps the model's output in a <c>candidates[0].content.parts[0].text</c> envelope.
        ///   </description></item>
        ///   <item><description>
        ///     The text extracted from that envelope is itself a JSON string, deserialized
        ///     into a <see cref="LogAnalysis"/>. If the model returns malformed JSON
        ///     (e.g. with unexpected markdown), the parse is swallowed and an empty
        ///     <see cref="LogAnalysis"/> is returned rather than surfacing a crash.
        ///   </description></item>
        /// </list>
        /// </remarks>
        /// <param name="logs">The log events to analyze. Each event should have a populated <c>Id</c>
        /// so the model can reference specific events in its <c>relatedEventIds</c> output.</param>
        /// <param name="modelOption">
        /// An integer corresponding to a <see cref="Enums.ModelOptions"/> value
        /// (e.g. 0 = Gemini, 1 = Custom) that controls which backend API route is called.
        /// </param>
        /// <param name="cancellationToken">Token for cancelling the HTTP request.</param>
        /// <returns>
        /// A <see cref="LogAnalysis"/> populated from the model response, or an empty
        /// <see cref="LogAnalysis"/> if the response could not be parsed.
        /// </returns>
        public async Task<LogAnalysis> AnalyzeLogsAsync(List<LogEvent> logs, int modelOption, CancellationToken cancellationToken = default)
        {
            // Convert the integer model option to its enum name (e.g. 0 → "Gemini")
            // so it can be interpolated into the route: /api/GeminiAPI/Query.
            var translatedModelOption = Enum.Parse<Enums.ModelOptions>(modelOption.ToString());
            var url = $"/api/{translatedModelOption}API/Query";

            var response = await httpClient.PostAsJsonAsync(url, logs, cancellationToken);

            // Throw immediately on non-2xx so callers receive a clear HttpRequestException
            // rather than a confusing null-analysis result.
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            // Step 1: unwrap the Gemini envelope to get the raw text the model produced.
            var responseObjects = JsonSerializer.Deserialize<GeminiResponse>(responseBody) ?? new GeminiResponse();
            var text = responseObjects.Candidates
                .FirstOrDefault()?.Content
                .Parts.FirstOrDefault()?.Text ?? "No response";

            // Step 2: parse the model's text output as a LogAnalysis JSON object.
            // The model is prompted to return only JSON, but if it includes markdown
            // fences or other formatting the parse will fail — return an empty analysis
            // rather than propagating the exception to the UI.
            LogAnalysis? analysis = null;
            try { analysis = JsonSerializer.Deserialize<LogAnalysis>(text); }
            catch (JsonException) { }

            return analysis ?? new LogAnalysis();
        }
    }
}