using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Controllers
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
        /// Gets a web page containing the last log output for this node.
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("last-log-output-for-node")]
        public IActionResult Stats()
        {
            string content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }
    }
}