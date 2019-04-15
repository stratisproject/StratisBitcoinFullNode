using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;

namespace Stratis.Bitcoin.Features.BlockStore.ControllersAndClients
{
    /// <summary>Rest client for <see cref="BlockStoreController"/>.</summary>
    public interface IBlockStoreClient
    {
        /// <summary><see cref="BlockStoreController.GetReceivedByAddress"/></summary>
        Task<Money> GetAddressBalanceAsync(string address, int minConfirmations, CancellationToken cancellation = default(CancellationToken));
    }

    /// <inheritdoc cref="IBlockStoreClient"/>
    public class BlockStoreClient : RestApiClientBase, IBlockStoreClient
    {
        public BlockStoreClient(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, int port)
            : base(loggerFactory, httpClientFactory, port, "BlockStore")
        {
        }

        /// <inheritdoc />
        public Task<Money> GetAddressBalanceAsync(string address, int minConfirmations, CancellationToken cancellation = default(CancellationToken))
        {
            string arguments = $"{nameof(address)}={address},{nameof(minConfirmations)}={minConfirmations}";

            return this.SendGetRequestAsync<Money>(BlockStoreRouteEndPoint.GetAddressBalance, arguments, cancellation);
        }
    }
}
