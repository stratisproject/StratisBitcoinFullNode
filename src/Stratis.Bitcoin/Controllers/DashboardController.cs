using System.Net;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.AsyncWork;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Controller providing HTML Dashboard
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public class DashboardController : Controller
    {
        private readonly IFullNode fullNode;
        private readonly IAsyncProvider asyncProvider;

        public DashboardController(IFullNode fullNode, IAsyncProvider asyncProvider)
        {
            this.fullNode = fullNode;
            this.asyncProvider = asyncProvider;
        }

        /// <summary>
        /// Gets a web page containing the last log output for this node.
        /// </summary>
        /// <returns>text/html content</returns>
        /// <response code="200">Returns webpage result</response>
        [HttpGet]
        [Route("Stats")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult Stats()
        {
            string content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }

        /// <summary>
        /// Returns a web page with Async Loops statistics
        /// </summary>
        /// <returns>text/html content</returns>
        /// <response code="200">Returns webpage result</response>
        [HttpGet]
        [Route("AsyncLoopsStats")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult AsyncLoopsStats()
        {
            return this.Content(this.asyncProvider.GetStatistics(false));
        }
    }
}