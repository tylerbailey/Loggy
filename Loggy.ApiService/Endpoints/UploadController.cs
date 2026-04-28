using Loggy.ApiService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Loggy.ApiService.Services
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        // POST api/<UploadController>
        [HttpPost]
        public async Task<IActionResult> UploadFile([FromForm] IFormFile? file)
        {
            if (file == null)
                return BadRequest("File is required.");

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string content = await reader.ReadToEndAsync().ConfigureAwait(false);

                List<LogEvent>? events = null;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    events = JsonSerializer.Deserialize<List<LogEvent>>(content);
                }

                events ??= new List<LogEvent>();

                // TODO: process events (save to DB, enqueue, etc.)
                return Ok(new { count = events.Count });
            }
        }      
    }
}
