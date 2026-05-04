using Loggy.Models;

namespace Loggy.ApiService.Services.Interfaces
{
    public interface IEventProcessorService
    {
        Dictionary<string, List<LogEvent>> SortEventsByException(List<LogEvent> events);
        Dictionary<string, List<LogEvent>> SortEventsByTimeStamp(List<LogEvent> events);
        Task<List<LogEvent>> GetEventsFromFile(IFormFile file);
    }
}
