using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestMaturedBlockSender : RestSenderBase, IMaturedBlockSender
    {
        public RestMaturedBlockSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, string route)
            : base(loggerFactory, settings, FederationGatewayController.ReceiveMaturedBlockRoute)
        {
        }

        /// <inheritdoc />
        public async Task SendMaturedBlockDepositsAsync(IMaturedBlockDeposits maturedBlockDeposits)
        {
            await this.SendAsync(maturedBlockDeposits).ConfigureAwait(false);
        }
    }
}
