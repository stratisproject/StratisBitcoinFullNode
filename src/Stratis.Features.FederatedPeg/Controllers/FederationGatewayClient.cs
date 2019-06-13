using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;

namespace Stratis.Features.FederatedPeg.Controllers
{
    /// <summary>Rest client for <see cref="FederationGatewayController"/>.</summary>
    public interface IFederationGatewayClient : IRestApiClientBase
    {
        /// <summary><see cref="FederationGatewayController.GetMaturedBlockDeposits"/></summary>
        /// <param name="model">A model containing the block height at which to start checking for matured blocks.</param>
        /// <param name="cancellation">A cancellation token to ensure that the task exists should it take too long.</param>
        /// <returns>An API result class containing a list of matured block deposits from a given height.</returns>
        Task<ApiResult<List<MaturedBlockDepositsModel>>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation);
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
        public async Task<ApiResult<List<MaturedBlockDepositsModel>>> GetMaturedBlockDepositsAsync(MaturedBlockRequestModel model, CancellationToken cancellation)
        {
            return await this.SendPostRequestAsync<MaturedBlockRequestModel, ApiResult<List<MaturedBlockDepositsModel>>>(model, FederationGatewayRouteEndPoint.GetMaturedBlockDeposits, cancellation);
        }
    }
}
