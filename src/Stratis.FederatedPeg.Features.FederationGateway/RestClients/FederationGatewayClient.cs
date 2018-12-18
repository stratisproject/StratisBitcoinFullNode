using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.FederatedPeg.Features.FederationGateway.Controllers;
using Stratis.FederatedPeg.Features.FederationGateway.Models;

namespace Stratis.FederatedPeg.Features.FederationGateway.RestClients
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public interface IFederationGatewayClient
    {
        /// <summary><see cref="FederationGatewayController.PushCurrentBlockTip"/></summary>
        Task PushCurrentBlockTipAsync(BlockTipModel model);

        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model);
    }

    /// <inheritdoc cref="IFederationGatewayClient"/>
    public class FederationGatewayClient : RestApiClientBase, IFederationGatewayClient
    {
        public FederationGatewayClient(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        /// <inheritdoc />
        public Task PushCurrentBlockTipAsync(BlockTipModel model)
        {
            return this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushCurrentBlockTip);
        }

        /// <inheritdoc />
        public Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model)
        {
            return this.SendPostRequestAsync<MaturedBlockRequestModel, List<MaturedBlockDepositsModel>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits);
        }
    }
}
