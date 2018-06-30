using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Controller providing HTML Dashboard
    /// </summary>
    [Route("")]
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
            string content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }
    }
}