using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Features.Api.Controllers
{
    [Route("api/[controller]")]
    public class NodeController : Controller
    {
        private readonly IFullNode fullNode;
        private readonly ApiFeatureOptions apiFeatureOptions;

        public NodeController(IFullNode fullNode, ApiFeatureOptions apiFeatureOptions)
        {
            this.fullNode = fullNode;
            this.apiFeatureOptions = apiFeatureOptions;
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
            this.fullNode.Stop();

            return this.Ok();
        }

        /// <summary>
        /// Set the keepalive flag.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("keepalive")]
        public IActionResult Keepalive()
        {
            if (this.apiFeatureOptions.KeepaliveMonitor == null)
                return new ObjectResult("Keepalive Disabled") {StatusCode = 405}; // (405) Method Not Allowed 

            this.apiFeatureOptions.KeepaliveMonitor.LastBeat = this.fullNode.DateTimeProvider.GetUtcNow();

            return this.Ok();
        }
    }
}