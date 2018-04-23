using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class StoreController : Controller
    {
        private readonly IBlockStoreCache blockStoreCache;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public StoreController(
            ILoggerFactory loggerFactory,
            IBlockStoreCache blockStoreCache,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.blockStoreCache = blockStoreCache;
            this.connectionManager = connectionManager;
            this.network = network;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
        }

        [Route("getblock")]
        [HttpGet]
        public async Task<IActionResult> GetBlockAsync([FromQuery] string blockHash, bool json = false)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockHash), blockHash);

            try
            {
                if (json)
                {
                    // TODO: format the block
                    return null;
                }
                else
                {
                    Block block = await this.blockStoreCache.GetBlockAsync(uint256.Parse(blockHash)).ConfigureAwait(false);
                    return this.Json(block);
                }
                
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}