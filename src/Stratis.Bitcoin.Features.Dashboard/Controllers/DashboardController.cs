using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.Dashboard.Controllers
{
    /// <summary>
    /// Controller providing HTML Dashboard
    /// </summary>
    [Route("[controller]")]
    public class DashboardController : Controller
    {
        private readonly IFullNode fullNode;

        public DashboardController(IFullNode fullNode)
        {
            this.fullNode = fullNode;
        }

        /// <summary>
        /// Returns a web page to act as a dashboard
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("")] // the endpoint name
        [Route("Stats")]
        public IActionResult Stats()
        {
            var content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }
    }
}