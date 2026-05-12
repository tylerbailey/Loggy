using Loggy.ApiService.Controllers.Interfaces;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models.Logs;
using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;


namespace Loggy.ApiService.Controllers.Classes
{
    [Route("api/[controller]")]
    [ApiController]
    public class LogEventProcessingController(IEventProcessingService eventProcessorService) : ControllerBase, IUploadController
    {
        private readonly IEventProcessingService _eventProcessingService = eventProcessorService;

        [HttpPost("ProcessEvents")]
        public async Task<IActionResult> ProcessEventsFromFile([FromForm] IFormFile file)
        {
            if (file == null)
                return BadRequest("File is required.");

            var events = await _eventProcessingService.GetEventsFromFile(file);
            var json = JsonSerializer.Serialize(events);
            return Content(json, "application/json");
        }

        [HttpPost("GetSortKeys")]
        public async Task<IActionResult> GetSortKeys(List<LogEvent> logEvents)
        {
            if (logEvents == null || logEvents.Count == 0)
                return BadRequest("Events are required.");

            var sortKeys = _eventProcessingService.GetSortKeys(logEvents);
            var json = JsonSerializer.Serialize(sortKeys);
            return Content(json, "application/json");
        }

        [HttpPost("GroupBy")]
        public async Task<IActionResult> GroupBy(List<LogEvent> logEvents, [FromQuery]string sortKey)
        {
            if (logEvents == null || logEvents.Count == 0)
                return BadRequest("Events are required.");

            var sortKeys = _eventProcessingService.GroupBy(logEvents, sortKey);
            var json = JsonSerializer.Serialize(sortKeys);
            return Content(json, "application/json");
        }


    }
}
