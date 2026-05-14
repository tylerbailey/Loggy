using Loggy.Models.Logs;
using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Mvc;

namespace Loggy.ApiService.Controllers.Interfaces
{
    public interface IUploadController
    {
         Task<IActionResult> ProcessEventsFromFile([FromForm] IFormFile file);
        Task<IActionResult> GetSortKeys(List<LogEvent> logEvents);
        Task<IActionResult> GroupBy(List<LogEvent> logEvents, [FromQuery] string sortKey);
    }
}
