using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers;
using Stratis.Features.Collateral.CounterChain;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.Controllers
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public interface IFederationGatewayClient : IRestApiClientBase
    {
        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDepositsAsync"/></summary>
        Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken));
    }

    /// <inheritdoc cref="IFederationGatewayClient"/>
    public class FederationGatewayClient : RestApiClientBase, IFederationGatewayClient
    {
        /// <summary>
        /// Currently the <paramref name="url"/> is required as it needs to be configurable for testing.
        /// <para>
        /// In a production/live scenario the sidechain and mainnet federation nodes should run on the same machine.
        /// </para>
        /// </summary>
        public FederationGatewayClient(ILoggerFactory loggerFactory, ICounterChainSettings settings, IHttpClientFactory httpClientFactory)
            : base(loggerFactory, httpClientFactory, settings.CounterChainApiPort, "FederationGateway", $"http://{settings.CounterChainApiHost}")
        {
        }

        /// <inheritdoc />
        public Task<List<MaturedBlockDepositsModel>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation = default(CancellationToken))
        {
            return this.SendPostRequestAsync<MaturedBlockRequestModel, List<MaturedBlockDepositsModel>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, cancellation);
        }
    }
}
