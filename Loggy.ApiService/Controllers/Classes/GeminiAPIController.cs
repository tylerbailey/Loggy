using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Loggy.ApiService.Controllers.Classes
{
    public class GeminiAPIController : Controller
    {
        private readonly IOptions<Options> _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string endpoint;

        public GeminiAPIController(IOptions<Options> options, IHttpClientFactory httpClientFactory)
        {
            _options = options;
            _httpClientFactory = httpClientFactory;
            endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_options.Value.ApiKey}";
        }

        [HttpGet("Query")]
        public IActionResult Query(string query)
        {
           var client = _httpClientFactory.CreateClient();
            var content = "";
            return Ok($"Received query: {query}");
        }
    }
}
