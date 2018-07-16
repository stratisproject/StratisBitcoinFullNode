using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    [Route("api/[controller]")]
    public class AppsController : Controller, IAppsController
    {
        private readonly ILogger logger;
        private readonly IAppsStore appsStore;

        public AppsController(ILoggerFactory loggerFactory, IAppsStore appsStore)
        {
            this.appsStore = appsStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [HttpGet]
        [Route("all")]
        public IActionResult GetApplications()
        {
            this.logger.LogInformation($"{nameof(this.GetApplications)}");

            return this.Ok(this.appsStore.Applications);
        }
    }
}
