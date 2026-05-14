using Loggy.Models.Logs;
using Loggy.Models.Logs.Classes;

namespace Loggy.ApiService.Services.Interfaces
{
    public interface IEventProcessingService
    {
        Dictionary<string, List<LogEvent>> GroupBy(List<LogEvent> events, string sortKey);
        Task<List<LogEvent>> GetEventsFromFile(IFormFile file);
        List<string> GetSortKeys(List<LogEvent> events);
    }
}
