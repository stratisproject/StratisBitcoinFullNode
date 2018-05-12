using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Dashboard.Controllers
{
    /// <summary>
    /// Controller providing HTML Dashboard
    /// </summary>
    [Route("dashboards")] //declares the endpoint
    public class DashboardController : Controller
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardController"/> class.
        /// </summary>
        public DashboardController()
        {
        }

        /// <summary>
        /// Returns a web page to act as a dashboard
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("nodedashboard")] // the endpoint name
		public IActionResult NodeDashboard()
        {
            return Content("Hello World", "text/html");
        }


    }
}