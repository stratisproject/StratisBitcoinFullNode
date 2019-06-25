using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestSharp;
using Stratis.FederatedSidechains.AdminDashboard.Entities;
using Stratis.FederatedSidechains.AdminDashboard.Filters;
using Stratis.FederatedSidechains.AdminDashboard.Services;
using Stratis.FederatedSidechains.AdminDashboard.Settings;

namespace Stratis.FederatedSidechains.AdminDashboard.Controllers
{
    [Route("stratis-node")]
    public class StratisNodeController : Controller
    {
        private readonly DefaultEndpointsSettings defaultEndpointsSettings;
        private readonly ApiRequester apiRequester;

        public StratisNodeController(IOptions<DefaultEndpointsSettings> defaultEndpointsSettings, ApiRequester apiRequester)
        {
            this.defaultEndpointsSettings = defaultEndpointsSettings.Value;
            this.apiRequester = apiRequester;
        }

        [Ajax]
        [HttpPost]
        [Route("resync")]
        public async Task<IActionResult> ResyncAsync(string value)
        {
            bool isHeight = int.TryParse(value, out _);
            if (isHeight)
            {
                ApiResponse getblockhashRequest = await this.apiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, $"/api/Consensus/getblockhash?height={value}");
                ApiResponse syncRequest = await this.apiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Wallet/sync", new { hash = ((string)getblockhashRequest.Content) });
                return syncRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
            }
            else
            {
                ApiResponse syncRequest = await this.apiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Wallet/sync", new { hash = value });
                return syncRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
            }
        }

        [Ajax]
        [Route("resync-crosschain-transactions")]
        public async Task<IActionResult> ResyncCrosschainTransactionsAsync()
        {
            //TODO: implement this method
            ApiResponse stopNodeRequest = await this.apiRequester.GetRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/status");
            return stopNodeRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
        }

        [Ajax]
        [Route("stop")]
        public async Task<IActionResult> StopNodeAsync()
        {
            ApiResponse stopNodeRequest = await this.apiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/stop", true);
            return stopNodeRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
        }

        [Ajax]
        [Route("change-log-level/{level}")]
        public async Task<IActionResult> ChangeLogLevelAsync(string rule, string level)
        {
            ApiResponse changeLogLevelRequest = await this.apiRequester.PostRequestAsync(this.defaultEndpointsSettings.StratisNode, "/api/Node/loglevels", new { logRules = new[] { new { ruleName = rule, logLevel = level } } }, Method.PUT);
            return changeLogLevelRequest.IsSuccess ? (IActionResult)Ok() : BadRequest();
        }
    }
}
