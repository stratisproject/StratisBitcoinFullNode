using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.Apps.Interfaces
{
    public interface IAppsController
    {
        IActionResult GetApplications();
    }
}
