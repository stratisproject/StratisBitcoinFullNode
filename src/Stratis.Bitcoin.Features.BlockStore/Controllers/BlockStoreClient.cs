using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>Rest client for <see cref="BlockStoreController"/>.</summary>
    public interface IBlockStoreClient
    {
        /// <summary><see cref="BlockStoreController.GetAddressBalance"/></summary>
        Task<Money> GetAddressBalanceAsync(string address, int minConfirmations, CancellationToken cancellation = default(CancellationToken));

        /// <summary><see cref="BlockStoreController.GetAddressesBalances"/></summary>
        Task<Dictionary<string, Money>> GetAddressBalancesAsync(IEnumerable<string> addresses, int minConfirmations, CancellationToken cancellation = default(CancellationToken));
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

        /// <inheritdoc />
        public Task<Dictionary<string, Money>> GetAddressBalancesAsync(IEnumerable<string> addresses, int minConfirmations, CancellationToken cancellation = default(CancellationToken))
        {
            string addrString = string.Join(",", addresses);

            string arguments = $"{nameof(addresses)}={addrString}&{nameof(minConfirmations)}={minConfirmations}";

            return this.SendGetRequestAsync<Dictionary<string, Money>>(BlockStoreRouteEndPoint.GetAddressesBalances, arguments, cancellation);
        }
    }
}
