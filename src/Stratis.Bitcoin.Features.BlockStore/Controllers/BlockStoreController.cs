using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.BlockStore.Controllers
{
    /// <summary>
    /// Controller providing operations on a blockstore.
    /// </summary>
    [Route("api/[controller]")]
    public class BlockStoreController : Controller
    {
        /// <see cref="IBlockStore"/>
        private readonly IBlockStore blockStore;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface that provides information about the chain and validation.</summary>
        private readonly IChainState chainState;

        /// <summary>
        /// The chain.
        /// </summary>
        private readonly ChainBase chain;

        /// <summary>
        /// Current network for the active controller instance.
        /// </summary>
        private readonly Network network;

        public BlockStoreController(Network network,
            ILoggerFactory loggerFactory,
            IBlockStore blockStore,
            IChainState chainState,
            ConcurrentChain chain)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(chainState, nameof(chainState));

            this.network = network;
            this.blockStore = blockStore;
            this.chainState = chainState;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Retrieves a given block given a block hash.
        /// </summary>
        /// <param name="query">A <see cref="SearchByHashRequest"/> model with a specific hash.</param>
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
                Block block = await this.blockStore.GetBlockAsync(uint256.Parse(query.Hash)).ConfigureAwait(false);

                if (block == null)
                {
                    return new NotFoundObjectResult("Block not found");
                }

                if (!query.OutputJson)
                {
                    return this.Json(block);
                }

                return query.ShowTransactionDetails
                    ? this.Json(new BlockTransactionDetailsModel(block, this.chain.GetBlock(block.GetHash()), this.chain.Tip, this.network))
                    : this.Json(new BlockModel(block, this.chain.GetBlock(block.GetHash()), this.chain.Tip, this.network));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the current consensus tip height.
        /// API implementation of RPC call.
        /// </summary>
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
    }
}