using Loggy.ApiService.Controllers.Interfaces;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models;
using Loggy.Models.Gemini;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Loggy.ApiService.Controllers.Classes
{
    /// <summary>
    /// Handles AI-powered log analysis by forwarding log events to the Google Gemini API
    /// and returning a structured analysis of patterns, errors, and recommendations.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class GeminiAPIController : Controller, IAIController
    {
        private readonly IOptions<Options> _options;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// The fully-qualified Gemini REST endpoint, including the API key from configuration.
        /// </summary>
        private readonly string endpoint;

        private readonly IEventProcessingService _eventProcessorService;

        /// <summary>
        /// Initializes the controller and builds the Gemini endpoint URL from configuration.
        /// </summary>
        /// <param name="options">App configuration containing the Gemini API key.</param>
        /// <param name="httpClientFactory">Factory used to create the outbound HTTP client.</param>
        /// <param name="eventProcessorService">Service for pre-processing log events before analysis.</param>
        public GeminiAPIController(IOptions<Options> options, IHttpClientFactory httpClientFactory, IEventProcessingService eventProcessorService)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _eventProcessorService = eventProcessorService;
            endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_options.Value.ApiKey}";
        }

        /// <summary>
        /// Accepts a list of log events, sends them to Gemini for analysis, and returns the raw
        /// Gemini response JSON. The model is prompted to return a structured object containing
        /// a summary, time range, detected patterns with severities and recommendations, and
        /// aggregate error counts.
        /// </summary>
        /// <param name="logs">The log events to analyze. Each event must have an <c>Id</c> field
        /// so Gemini can reference specific events in its <c>relatedEventIds</c> output.</param>
        /// <returns>
        /// <c>200 OK</c> with the raw Gemini JSON response body, or
        /// <c>400 Bad Request</c> with the Gemini error body if the upstream call fails.
        /// </returns>
        [HttpPost("Query")]
        public async Task<IActionResult> QueryAsync(List<LogEvent> logs)
        {
            // Serialize the log events to JSON for embedding in the prompt.
            // UnsafeRelaxedJsonEscaping is used so characters like '<', '>', and '&' are
            // passed through as-is rather than being Unicode-escaped, keeping the prompt readable.
            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var client = _httpClientFactory.CreateClient();

            // Gemini can be slow on large log payloads, so a generous timeout is set
            // to avoid premature cancellation.
            client.Timeout = TimeSpan.FromSeconds(120);

            // Build the Gemini request, wrapping the prompt in the required
            // contents > parts structure expected by the generateContent API.
            var request = new GeminiRequest
            {
                Contents = new List<GeminiContentPart>
                {
                    new GeminiContentPart
                    {
                        Parts = new List<GeminiPart>
                        {
                            new() {
                                // The prompt instructs Gemini to return only a JSON object with a
                                // fixed schema (no markdown fences) so the response can be parsed
                                // directly by the client without further cleanup.
                                Text = $@"Analyze the following logs and respond ONLY with a JSON object in this exact format, no markdown, no backticks:
                                        {{
                                            ""summary"": ""brief overall summary"",
                                            ""timeRange"": ""start time to end time"",
                                            ""patterns"": [
                                                {{
                                                    ""title"": ""pattern name"",
                                                    ""severity"": ""Critical|High|Medium|Low"",
                                                    ""description"": ""what is happening"",
                                                    ""recommendation"": ""what to do about it"",
                                                    ""relatedEventIds"": [1, 2, 3]
                                                }}
                                            ],
                                            ""errorCounts"": {{
                                                ""critical"": 0,
                                                ""warnings"": 0,
                                                ""errors"": 0,
                                                ""info"": 0
                                            }}
                                        }}
                                        Each log event has an Id field. Use those Id values in relatedEventIds to reference the specific events that belong to each pattern.
                                        Logs: {json}"
                            }
                        }
                    }
                }
            };

            // Serialize the full Gemini request body with the same relaxed encoder
            // so any special characters in the embedded log JSON survive double-serialization.
            var serializedQuery = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var response = await client.PostAsync(endpoint, new StringContent(serializedQuery, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                // Surface the full Gemini error body so callers can diagnose quota,
                // auth, or payload issues without needing to inspect raw HTTP traffic.
                var errorBody = await response.Content.ReadAsStringAsync();
                return BadRequest($"Gemini error: {errorBody}");
            }

            // Return the raw Gemini response; the client is responsible for
            // deserializing it into a LogAnalysis object.
            return Ok(await response.Content.ReadAsStringAsync());
        }
    }
}