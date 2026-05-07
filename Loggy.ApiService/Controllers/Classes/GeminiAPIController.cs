using Loggy.ApiService.Services.Interfaces;
using Loggy.Models;
using Loggy.Models.Gemini;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Loggy.ApiService.Controllers.Classes
{
    [Route("api/[controller]")]
    [ApiController]
    public class GeminiAPIController : Controller
    {
        private readonly IOptions<Options> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string endpoint;
        private readonly IEventProcessingService _eventProcessorService;
        public GeminiAPIController(IOptions<Options> options, IHttpClientFactory httpClientFactory, IEventProcessingService eventProcessorService)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            _eventProcessorService = eventProcessorService;
            endpoint = endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_options.Value.ApiKey}";
        }

        [HttpPost("Query")]
        public async Task<IActionResult> QueryAsync(List<LogEvent> logs)
        {

            var json = JsonSerializer.Serialize(logs, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });


            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);
            var request = new GeminiRequest
            {
                Contents = new List<GeminiContentPart>
        {
            new GeminiContentPart
            {
                Parts = new List<GeminiPart>
                {
                   new() {
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


            var serializedQuery = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            var response = await client.PostAsync(endpoint, new StringContent(serializedQuery, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return BadRequest($"Gemini error: {errorBody}");
            }

            return Ok(await response.Content.ReadAsStringAsync());
        }
    }
}
