using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api.Controllers
{
    [Route("api/[controller]")]
    public class NodeController : Controller
    {
        private readonly IFullNode fullNode;

        private readonly ApiSettings apiSettings;

        public NodeController(IFullNode fullNode, ApiSettings apiSettings)
        {
            this.fullNode = fullNode;
            this.apiSettings = apiSettings;
        }

        /// <summary>
        /// Returns some general information about the status of the underlying node.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("status")]
        public IActionResult Status()
        {
            return this.NotFound();
        }

        /// <summary>
        /// Trigger a shoutdown of the current running node.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("shutdown")]
        public IActionResult Shutdown()
        {
            // start the node shutdown process
            this.fullNode.Dispose();

            return this.Ok();
        }

        /// <summary>
        /// Set the keepalive flag.
        /// </summary>
        /// <returns>An HTTP status code indicating success (200).</returns>
        [HttpPost]
        [Route("keepalive")]
        public IActionResult Keepalive()
        {
            if (this.apiSettings.KeepaliveTimer == null)
                return new ObjectResult("Keepalive Disabled") { StatusCode = 405 }; // (405) Method Not Allowed

            this.apiSettings.KeepaliveTimer.Reset();
            return this.Ok();
        }
    }
}
