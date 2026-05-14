using Loggy.Models.Logs;
using Loggy.Models.Logs.Classes;
using Microsoft.AspNetCore.Mvc;

namespace Loggy.ApiService.Controllers.Interfaces
{
    public interface IAIController
    {
        Task<IActionResult> QueryAsync(List<LogEvent> logs);
    }
}
