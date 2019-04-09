using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>Controller providing operations on a blockstore.</summary>
    [Route("api/[controller]")]
    public class BlockStoreController : Controller
    {
        /// <see cref="IBlockStore"/>
        private readonly IBlockStore blockStore;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface that provides information about the chain and validation.</summary>
        private readonly IChainState chainState;

        /// <summary>The chain.</summary>
        private readonly ChainIndexer chainIndexer;

        /// <summary>Current network for the active controller instance.</summary>
        private readonly Network network;

        private readonly IAddressIndexer addressIndexer;

        public BlockStoreController(
            Network network,
            ILoggerFactory loggerFactory,
            IBlockStore blockStore,
            IChainState chainState,
            ChainIndexer chainIndexer,
            IAddressIndexer addressIndexer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(addressIndexer, nameof(addressIndexer));

            this.addressIndexer = addressIndexer;
            this.network = network;
            this.blockStore = blockStore;
            this.chainState = chainState;
            this.chainIndexer = chainIndexer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Retrieves the block which matches the supplied block hash.
        /// </summary>
        /// <param name="query">An object containing the necessary parameters to search for a block.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [Route("block")]
        [HttpGet]
        public async Task<IActionResult> GetBlockAsync([FromQuery] SearchByHashRequest query)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Block block = this.blockStore.GetBlock(uint256.Parse(query.Hash));

                if (block == null)
                {
                    return new NotFoundObjectResult("Block not found");
                }

                if (!query.OutputJson)
                {
                    return this.Json(block);
                }

                return query.ShowTransactionDetails
                    ? this.Json(new BlockTransactionDetailsModel(block, this.chainIndexer.GetHeader(block.GetHash()), this.chainIndexer.Tip, this.network))
                    : this.Json(new BlockModel(block, this.chainIndexer.GetHeader(block.GetHash()), this.chainIndexer.Tip, this.network));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the current consensus tip height.
        /// </summary>
        /// <remarks>This is an API implementation of an RPC call.</remarks>
        /// <returns>The current tip height. Returns <c>null</c> if fails. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [Route("getblockcount")]
        [HttpGet]
        public IActionResult GetBlockCount()
        {
            try
            {
                return this.Json(this.chainState.ConsensusTip.Height);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>Provides balance of the given address confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        [Route("getaddressbalance")]
        [HttpGet]
        public IActionResult GetAddressBalance([FromQuery] string address, int minConfirmations)
        {
            try
            {
                return this.Json(this.addressIndexer.GetAddressBalance(address, minConfirmations));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>Returns the total amount received by the given address in transactions with at least<paramref name= "minConfirmations" /> confirmations.</ summary >
        [Route("getreceivedbyaddress")]
        [HttpGet]
        public IActionResult GetReceivedByAddress([FromQuery] string address, int minConfirmations)
        {
            try
            {
                return this.Json(this.addressIndexer.GetReceivedByAddress(address, minConfirmations));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}