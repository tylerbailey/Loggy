using Loggy.Models;
using Microsoft.AspNetCore.Mvc;

namespace Loggy.ApiService.Controllers.Interfaces
{
    public interface IAIController
    {
        Task<IActionResult> QueryAsync(List<LogEvent> logs);
    }
}
