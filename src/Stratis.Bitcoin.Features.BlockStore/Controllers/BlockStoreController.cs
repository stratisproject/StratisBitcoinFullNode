using System;
using System.Net;
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
    public static class BlockStoreRouteEndPoint
    {
        public const string GetAddressesBalances = "getaddressesbalances";
        public const string GetVerboseAddressesBalances = "getverboseaddressesbalances";
        public const string GetAddressIndexerTip = "addressindexertip";
        public const string GetBlock = "block";
        public const string GetBlockCount = "GetBlockCount";
    }

    /// <summary>Controller providing operations on a blockstore.</summary>
    [Route("api/[controller]")]
    public class BlockStoreController : Controller
    {
        private readonly IAddressIndexer addressIndexer;

        /// <summary>Provides access to the block store on disk.</summary>
        private readonly IBlockStore blockStore;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface that provides information about the chain and validation.</summary>
        private readonly IChainState chainState;

        /// <summary>The chain.</summary>
        private readonly ChainIndexer chainIndexer;

        /// <summary>Current network for the active controller instance.</summary>
        private readonly Network network;

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
        /// Retrieves the <see cref="addressIndexer"/>'s tip.
        /// </summary>
        /// <returns>An instance of <see cref="AddressIndexerTipModel"/> containing the tip's hash and height.</returns>
        [Route(BlockStoreRouteEndPoint.GetAddressIndexerTip)]
        [HttpGet]
        public IActionResult GetAddressIndexerTip()
        {
            try
            {
                ChainedHeader addressIndexerTip = this.addressIndexer.IndexerTip;
                return this.Json(new AddressIndexerTipModel() { TipHash = addressIndexerTip?.HashBlock, TipHeight = addressIndexerTip?.Height });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves the block which matches the supplied block hash.
        /// </summary>
        /// <param name="query">An object containing the necessary parameters to search for a block.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [Route(BlockStoreRouteEndPoint.GetBlock)]
        [HttpGet]
        public IActionResult GetBlock([FromQuery] SearchByHashRequest query)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            try
            {
                uint256 blockId = uint256.Parse(query.Hash);

                ChainedHeader chainedHeader = this.chainIndexer.GetHeader(blockId);

                if (chainedHeader == null)
                    return this.Ok("Block not found");

                Block block = chainedHeader.Block ?? this.blockStore.GetBlock(blockId);

                // In rare occasions a block that is found in the
                // indexer may not have been pushed to the store yet. 
                if (block == null)
                    return this.Ok("Block not found");

                if (!query.OutputJson)
                {
                    return this.Json(block);
                }

                BlockModel blockModel = query.ShowTransactionDetails
                    ? new BlockTransactionDetailsModel(block, chainedHeader, this.chainIndexer.Tip, this.network)
                    : new BlockModel(block, chainedHeader, this.chainIndexer.Tip, this.network);

                if (this.network.Consensus.IsProofOfStake)
                {
                    var posBlock = block as PosBlock;

                    blockModel.PosBlockSignature = posBlock.BlockSignature.ToHex(this.network);
                    blockModel.PosBlockTrust = new Target(chainedHeader.GetBlockProof()).ToUInt256().ToString();
                    blockModel.PosChainTrust = chainedHeader.ChainWork.ToString(); // this should be similar to ChainWork
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
        /// Gets the current consensus tip height.
        /// </summary>
        /// <remarks>This is an API implementation of an RPC call.</remarks>
        /// <returns>The current tip height. Returns <c>null</c> if fails. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [Route(BlockStoreRouteEndPoint.GetBlockCount)]
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

        /// <summary>Provides balance of the given addresses confirmed with at least <paramref name="minConfirmations"/> confirmations.</summary>
        /// <param name="addresses">A comma delimited set of addresses that will be queried.</param>
        /// <param name="minConfirmations">Only blocks below consensus tip less this parameter will be considered.</param>
        /// <returns>A result object containing the balance for each requested address and if so, a meesage stating why the indexer is not queryable.</returns>
        [Route(BlockStoreRouteEndPoint.GetAddressesBalances)]
        [HttpGet]
        public IActionResult GetAddressesBalances(string addresses, int minConfirmations)
        {
            try
            {
                string[] addressesArray = addresses.Split(',');

                this.logger.LogDebug("Asking data for {0} addresses.", addressesArray.Length);

                AddressBalancesResult result = this.addressIndexer.GetAddressBalances(addressesArray, minConfirmations);

                this.logger.LogDebug("Sending data for {0} addresses.", result.Balances.Count);

                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }


        /// <summary>Provides verbose balance data of the given addresses.</summary>
        /// <param name="addresses">A comma delimited set of addresses that will be queried.</param>
        /// <returns>A result object containing the balance for each requested address and if so, a meesage stating why the indexer is not queryable.</returns>
        [Route(BlockStoreRouteEndPoint.GetVerboseAddressesBalances)]
        [HttpGet]
        public IActionResult GetVerboseAddressesBalancesData(string addresses)
        {
            try
            {
                string[] addressesArray = addresses?.Split(',') ?? new string[] { };

                this.logger.LogDebug("Asking data for {0} addresses.", addressesArray.Length);

                VerboseAddressBalancesResult result = this.addressIndexer.GetAddressIndexerState(addressesArray);

                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
