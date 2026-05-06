using Loggy.Models;

namespace Loggy.ApiService.Services.Interfaces
{
    public interface IEventProcessorService
    {
        Dictionary<string, List<SeriLogEvent>> SortEventsByException(List<SeriLogEvent> events);
        Dictionary<string, List<SeriLogEvent>> SortEventsByTimeStamp(List<SeriLogEvent> events);
        Task<List<SeriLogEvent>> GetEventsFromFile(IFormFile file);
    }
}
