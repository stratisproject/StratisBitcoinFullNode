using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.AsyncWork;

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
        private readonly IAsyncProvider asyncProvider;

        public DashboardController(IFullNode fullNode, IAsyncProvider asyncProvider)
        {
            this.fullNode = fullNode;
            this.asyncProvider = asyncProvider;
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

        /// <summary>
        /// Returns a web page with Async Loops statistics
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("AsyncLoopsStats")]
        public IActionResult AsyncLoopsStats()
        {
            return this.Content(this.asyncProvider.GetStatistics(false));
        }

        /// <summary>
        /// Returns a web page with Async Loops statistics
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("CreateFaultyLoops")]
        public void CreateFaultyLoops()
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(this.fullNode.NodeLifetime.ApplicationStopping);
            cts.CancelAfter(10000); // cancel the loops after 10 seconds

            var dequeuer = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<int>("Example of Faulty Dequeuer", (item, ct) =>
            {
                throw new System.Exception("Ouch! What an error! Fix that please (joking).");
            });

            //this will trigger the exception
            dequeuer.Enqueue(0);

            this.asyncProvider.CreateAndRunAsyncLoop("Example of Faulty Loop", (ct) =>
            {
                throw new System.Exception("Ouch! What an error! Fix that please (joking).");
            }, CancellationToken.None);
        }
    }
}