using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestBlockTipSender : RestSenderBase, IBlockTipSender
    {
        public RestBlockTipSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        public async Task SendBlockTipAsync(IBlockTip blockTip)
        {
            if (this.CanSend())
            {
                await this.SendAsync((BlockTipModel)blockTip, FederationGatewayRouteEndPoint.ReceiveCurrentBlockTip).ConfigureAwait(false);
            }
        }
    }
}
