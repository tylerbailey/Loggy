using Microsoft.AspNetCore.Mvc;

namespace Loggy.ApiService.Controllers.Interfaces
{
    public interface IUploadController
    {
         Task<IActionResult> SortByException([FromForm] IFormFile file);
        Task<IActionResult> SortByTimeStamp([FromForm] IFormFile file);
    }
}
