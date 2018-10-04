using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.BlockStore.V2.Controllers
{
    /// <summary>
    /// Controller providing operations on a blockstore.
    /// </summary>
    [ApiVersion("2.0")]
    [Route("api/blocks")]
    public class BlockStoreController : Controller
    {
        /// <summary>An interface for getting blocks asynchronously from the blockstore cache.</summary>
        private readonly IBlockStore blockStoreCache;
        
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface that provides information about the chain and validation.</summary>
        private readonly IChainState chainState;

        private readonly IStakeChain stakeChain;

        /// <summary>
        /// Current network for the active controller instance.
        /// </summary>
        private readonly Network network;

        private readonly ConcurrentChain chain;

        public BlockStoreController(
            Network network,
            ILoggerFactory loggerFactory,
            IBlockStore blockStoreCache,
            IStakeChain stakeChain,
            ConcurrentChain chain,
            IChainState chainState)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(chainState, nameof(chainState));

            this.network = network;
            this.blockStoreCache = blockStoreCache;
            this.stakeChain = stakeChain;
            this.chain = chain;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Retrieves a given block given a block hash.
        /// </summary>
        /// <param name="query">A <see cref="SearchByHashRequest"/> model with a specific hash.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(V2.Models.BlockModel[]), 200)]
        public async Task<IActionResult> GetBlocksAsync()
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            var pageSize = 10; // Should we allow page size to be set in query?
            
            try
            {
                var currentDownloadedHeight = this.chainState.BlockStoreTip.Height;
                var blocks = new List<V2.Models.BlockModel>();
                var chainHeader = this.chainState.BlockStoreTip;

                while (chainHeader != null && blocks.Count < pageSize)
                {
                    var block = await this.blockStoreCache.GetBlockAsync(chainHeader.HashBlock).ConfigureAwait(false);

                    var blockModel = new V2.Models.BlockModel(block, chainHeader.Height, this.network);
                    blocks.Add(blockModel);

                    chainHeader = chainHeader.Previous;
                }
                
                return this.Json(blocks);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves a given block given a block hash or block height.
        /// </summary>
        /// <param name="query">A <see cref="SearchByHashRequest"/> model with a specific hash.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(V2.Models.BlockModel), 200)]
        [ProducesResponseType(typeof(void), 404)]
        //[ProducesResponseType(typeof(void), 401)]
        public async Task<IActionResult> GetBlockAsync(string id, [FromQuery] BlockQueryRequest query)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            ChainedHeader chainHeader = null;
            

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id", "id must be block hash or block height");
            }

            // If the id is more than 50 characters, it is likely hash and not height.
            if (id.Length < 50)
            {
                chainHeader = this.chain.GetBlock(int.Parse(id));
            }
            else
            {
                chainHeader = this.chain.GetBlock(new uint256(id));
            }

            try
            {
                BlockStake blockStake = this.stakeChain.Get(chainHeader.HashBlock);
                Block block = await this.blockStoreCache.GetBlockAsync(chainHeader.Header.GetHash()).ConfigureAwait(false);

                if (block == null) return new NotFoundObjectResult("Block not found");

                V2.Models.BlockModel blockModel = new V2.Models.BlockModel(block, chainHeader.Height, this.network);

                if (blockStake != null)
                {
                    blockModel.StakeTime = blockStake.StakeTime;
                    blockModel.StakeModifierV2 = blockStake.StakeModifierV2;
                    blockModel.HashProof = blockStake.HashProof;
                }

                return this.Json(blockModel);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves a given block given a block hash.
        /// </summary>
        /// <param name="query">A <see cref="SearchByHashRequest"/> model with a specific hash.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [HttpGet, Route("page/{index}")]
        public async Task<IActionResult> GetBlocksPageAsync(int index, [FromQuery] BlockQueryRequest query)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            throw new NotImplementedException("Not implemented");
        }

        /// <summary>
        /// Gets the current consensus tip height.
        /// API implementation of RPC call.
        /// </summary>
        /// <returns>The current tip height. Returns <c>null</c> if fails. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [HttpGet, Route("count")]
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