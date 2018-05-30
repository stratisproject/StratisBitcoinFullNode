using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Consensus
{
    [Route("api/[controller]")]
    public class ConsensusController : FeatureController
    {
        private readonly ILogger logger;

        public IConsensusLoop ConsensusLoop { get; private set; }

        public ConsensusController(ILoggerFactory loggerFactory, IChainState chainState = null,
            IConsensusLoop consensusLoop = null, ConcurrentChain chain = null)
            : base(chainState: chainState, chain: chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.ConsensusLoop = consensusLoop;
        }

        [ActionName("getbestblockhash")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Get the hash of the block at the consensus tip.")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            return this.ChainState?.ConsensusTip?.HashBlock;
        }

        /// <summary>
        /// Get the hash of the block at the consensus tip.
        /// API implementation of RPC call.
        /// </summary>
        /// <returns>Json formatted <see cref="uint256"/> of best block hash. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getbestblockhash")]
        [HttpGet]
        public IActionResult GetBestBlockHashAPI()
        {
            try
            {
                return this.Json(this.GetBestBlockHash());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [ActionName("getblockhash")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionDescription("Gets the hash of the block at the given height.")]
        public uint256 GetBlockHash(int height)
        {
            Guard.NotNull(this.ConsensusLoop, nameof(this.ConsensusLoop));
            Guard.NotNull(this.Chain, nameof(this.Chain));

            this.logger.LogDebug("RPC GetBlockHash {0}", height);

            uint256 bestBlockHash = this.ConsensusLoop.Tip?.HashBlock;
            ChainedHeader bestBlock = bestBlockHash == null ? null : this.Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedHeader block = this.Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }

        /// <summary>
        /// Gets the hash of the block at the given height.
        /// API implementation of RPC call.
        /// </summary>
        /// <param name="request">A <see cref="GetBlockHashRequestModel"/> request containing the height.</param>
        /// <returns>Json formatted <see cref="uint256"/> hash of the block at the given height. Returns <see cref="IActionResult"/> formatted error if fails.</returns>
        [Route("getblockhash")]
        [HttpGet]
        public IActionResult GetBlockHash(GetBlockHashRequestModel request)
        {
            try
            {
                int height;
                if (!int.TryParse(request.height, out height))
                {
                    throw new ArgumentException(nameof(request.height));
                }
                return this.Json(this.GetBlockHash(height));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
