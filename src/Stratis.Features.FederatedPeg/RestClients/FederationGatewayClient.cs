using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Features.FederatedPeg.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.RestClients
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public interface IFederationGatewayClient
    {
        /// <summary><see cref="FederationGatewayController.PushCurrentBlockTip"/></summary>
        Task<HttpResponseMessage> PushCurrentBlockTipAsync(BlockTipModel model, CancellationToken cancellation = default(CancellationToken));

        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken));
    }

    /// <inheritdoc cref="IFederationGatewayClient"/>
    public class FederationGatewayClient : RestApiClientBase, IFederationGatewayClient
    {
        public FederationGatewayClient(ILoggerFactory loggerFactory, IFederationGatewaySettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, settings, httpClientFactory)
        {
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> PushCurrentBlockTipAsync(BlockTipModel model, CancellationToken cancellation = default(CancellationToken))
        {
            return this.SendPostRequestAsync(model, FederationGatewayRouteEndPoint.PushCurrentBlockTip, cancellation);
        }

        /// <inheritdoc />
        public Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken))
        {
            return this.SendPostRequestAsync<MaturedBlockRequestModel, List<MaturedBlockDepositsModel>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, cancellation);
        }
    }
}
