using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Controllers.Models;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>Rest client for <see cref="BlockStoreController"/>.</summary>
    public interface IBlockStoreClient : IRestApiClientBase
    {
        /// <summary><see cref="BlockStoreController.GetVerboseAddressesBalancesData"/></summary>
        Task<VerboseAddressBalancesResult> GetVerboseAddressesBalancesDataAsync(IEnumerable<string> addresses, CancellationToken cancellation = default(CancellationToken));
    }

    /// <inheritdoc cref="IBlockStoreClient"/>
    public class BlockStoreClient : RestApiClientBase, IBlockStoreClient
    {
        /// <summary>
        /// Currently the <paramref name="url"/> is required as it needs to be configurable for testing.
        /// <para>
        /// In a production/live scenario the sidechain and mainnet federation nodes should run on the same machine.
        /// </para>
        /// </summary>
        public BlockStoreClient(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, string url, int port)
            : base(loggerFactory, httpClientFactory, port, "BlockStore", url)
        {
        }

        /// <inheritdoc />
        public Task<VerboseAddressBalancesResult> GetVerboseAddressesBalancesDataAsync(IEnumerable<string> addresses, CancellationToken cancellation = default(CancellationToken))
        {
            string addrString = string.Join(",", addresses);

            string arguments = $"{nameof(addresses)}={addrString}";

            return this.SendGetRequestAsync<VerboseAddressBalancesResult>(BlockStoreRouteEndPoint.GetVerboseAddressesBalances, arguments, cancellation);
        }
    }
}