using Loggy.ApiService.Controllers.Interfaces;
using Loggy.ApiService.Services.Interfaces;
using Loggy.Models.Logs;
using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;


namespace Loggy.ApiService.Controllers.Classes
{
    /// <summary>
    /// Handles ingestion and pre-processing of uploaded log files.
    /// Exposes endpoints for parsing a file into log events, retrieving the
    /// available sort/group keys, and grouping events by a chosen key.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class LogEventProcessingController(IEventProcessingService eventProcessorService) : ControllerBase, IUploadController
    {
        private readonly IEventProcessingService _eventProcessingService = eventProcessorService;

        /// <summary>
        /// Parses an uploaded log file and returns the events as a JSON array.
        /// Each event is assigned a sequential <c>Id</c> starting at 1.
        /// </summary>
        /// <param name="file">The log file submitted as multipart/form-data.</param>
        /// <returns>
        /// <c>200 OK</c> with a JSON array of <see cref="LogEvent"/> objects, or
        /// <c>400 Bad Request</c> if no file was provided.
        /// </returns>
        [HttpPost("ProcessEvents")]
        public async Task<IActionResult> ProcessEventsFromFile([FromForm] IFormFile file)
        {
            if (file == null)
                return BadRequest("File is required.");

            var events = await _eventProcessingService.GetEventsFromFile(file);
            var json = JsonSerializer.Serialize(events);
            return Content(json, "application/json");
        }

        /// <summary>
        /// Returns the list of field names (keys) available for sorting or grouping,
        /// derived from the schema of the first event in the supplied list.
        /// </summary>
        /// <param name="logEvents">The events previously returned by <c>ProcessEvents</c>.</param>
        /// <returns>
        /// <c>200 OK</c> with a JSON array of key name strings, or
        /// <c>400 Bad Request</c> if the event list is null or empty.
        /// </returns>
        [HttpPost("GetSortKeys")]
        public async Task<IActionResult> GetSortKeys(List<LogEvent> logEvents)
        {
            if (logEvents == null || logEvents.Count == 0)
                return BadRequest("Events are required.");

            var sortKeys = _eventProcessingService.GetSortKeys(logEvents);
            var json = JsonSerializer.Serialize(sortKeys);
            return Content(json, "application/json");
        }

        /// <summary>
        /// Groups the supplied log events by the value of a specified schema field,
        /// returning a dictionary keyed by that field's distinct values.
        /// Events that do not contain the field are grouped under <c>"Undefined"</c>.
        /// </summary>
        /// <param name="logEvents">The events to group.</param>
        /// <param name="sortKey">The schema field name to group by (passed as a query parameter).</param>
        /// <returns>
        /// <c>200 OK</c> with a JSON object mapping each distinct field value to its list of events, or
        /// <c>400 Bad Request</c> if the event list is null or empty.
        /// </returns>
        [HttpPost("GroupBy")]
        public async Task<IActionResult> GroupBy(List<LogEvent> logEvents, [FromQuery] string sortKey)
        {
            if (logEvents == null || logEvents.Count == 0)
                return BadRequest("Events are required.");

            var sortKeys = _eventProcessingService.GroupBy(logEvents, sortKey);
            var json = JsonSerializer.Serialize(sortKeys);
            return Content(json, "application/json");
        }
    }
}