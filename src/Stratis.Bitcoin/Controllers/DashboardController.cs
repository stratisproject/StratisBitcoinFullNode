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
        /// Provides simple navigation
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("")]
        public IActionResult Home()
        {
            string statsLink = "<a href='Stats'>Node Stats</a>";
            string logsLink = "<a href='Logs'>Node Logs</a>";

            return this.Content($"{statsLink} <br/><br/> {logsLink}", "text/html");
        }

        /// <summary>
        /// Returns a web page to act as a dashboard
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("Stats")]
        public IActionResult Stats()
        {
            string content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }
    }
}