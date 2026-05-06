using Loggy.ApiService.Controllers.Interfaces;
using Loggy.ApiService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;


namespace Loggy.ApiService.Controllers.Classes
{
    [Route("api/[controller]")]
    [ApiController]
    public class SerilogUploadController(IEventProcessorService eventProcessorService) : ControllerBase, IUploadController
    {
        private readonly IEventProcessorService _eventProcessorService = eventProcessorService;

        [HttpPost("SortByException")]
        public async Task<IActionResult> SortByException([FromForm] IFormFile file)
        {
            if (file == null)
                return BadRequest("File is required.");

            var events = _eventProcessorService.SortEventsByException(await _eventProcessorService.GetEventsFromFile(file));
            var json = JsonSerializer.Serialize(events);
            return Content(json, "application/json");

        }

        [HttpPost("SortByTimeStamp")]
        public async Task<IActionResult> SortByTimeStamp([FromForm] IFormFile file)
        {
            if (file == null)
                return BadRequest("File is required.");

            var events = _eventProcessorService.SortEventsByTimeStamp(await _eventProcessorService.GetEventsFromFile(file));
            var json = JsonSerializer.Serialize(events);
            return Content(json, "application/json");
        }
    }
}
