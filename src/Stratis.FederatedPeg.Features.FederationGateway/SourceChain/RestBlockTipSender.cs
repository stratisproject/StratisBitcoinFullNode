using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestBlockTipSender : RestSenderBase, IBlockTipSender
    {
        public RestBlockTipSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, string route)
            : base(loggerFactory, settings, FederationGatewayController.ReceiveCurrentBlockTipRoute)
        {
        }

        public async Task SendBlockTipAsync(IBlockTip blockTip)
        {
            await this.SendAsync(blockTip).ConfigureAwait(false);
        }
    }
}
