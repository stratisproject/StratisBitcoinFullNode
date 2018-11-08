using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.SourceChain
{
    public class RestMaturedBlockSender : RestSenderBase, IMaturedBlockSender
    {
        public RestMaturedBlockSender(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        /// <inheritdoc />
        public async Task SendMaturedBlockDepositsAsync(IMaturedBlockDeposits maturedBlockDeposits)
        {
            await this.SendAsync((MaturedBlockDepositsModel)maturedBlockDeposits, FederationGatewayController.ReceiveMaturedBlockRoute).ConfigureAwait(false);
        }
    }
}
