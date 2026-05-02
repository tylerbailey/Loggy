using Loggy.ApiService.Services.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;


namespace Loggy.ApiService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        // POST api/<UploadController>
        [HttpPost("uploadlog")]

        public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
        {
            var processer = new EventProcessorService();

            if (file == null)
                return BadRequest("File is required.");

            var events = processer.SortEventsByException(await processer.GetEventsFromFile(file));
            var json = JsonSerializer.Serialize(events);
            return Content(json, "application/json");
        }
    }
}
