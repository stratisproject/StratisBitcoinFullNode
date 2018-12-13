using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using Stratis.FederatedSidechains.AdminDashboard.Rest;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    [Route("stratis-node")]
    public class StratisNodeController : Controller
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;

        public StratisNodeController(IOptions<DefaultEndpointsSettings> defaultEndpointsSettings)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
        }

        [Ajax]
        [HttpPost]
        [Route("resync")]
        public async Task<IActionResult> ResyncAsync(string value)
        {   
            bool isHeight = int.TryParse(value, out int height);
            if(isHeight)
            {
                ApiResponse getblockhashRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, $"/api/Consensus/getblockhash?height={value}");
                ApiResponse syncRequest = await ApiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Wallet/sync", new {hash=((string)getblockhashRequest.Content)});
                return syncRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
            }
            else
            {
                ApiResponse syncRequest = await ApiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Wallet/sync", new {hash=value});
                return syncRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
            }
        }

        [Ajax]
        [Route("resync-crosschain-transactions")]
        public async Task<IActionResult> ResyncCrosschainTransactionsAsync()
        {
            //TODO: implement this method
            ApiResponse stopNodeRequest = await ApiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
        }

        [Ajax]
        [Route("stop")]
        public async Task<IActionResult> StopNodeAsync()
        {
            ApiResponse stopNodeRequest = await ApiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/stop", true);
            return stopNodeRequest.IsSuccess ? (IActionResult) Ok() : BadRequest();
        }
    }
}
