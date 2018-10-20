using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using City.Features.BlockExplorer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace City.Features.BlockExplorer.Controllers
{
    /// <summary>
    /// Controller providing operations on a blockstore.
    /// </summary>
    [ApiVersion("2.0")]
    [Route("api/transactions")]
    public class TransactionStoreController : Controller
    {
        /// <summary>An interface for getting blocks asynchronously from the blockstore cache.</summary>
        private readonly IBlockStore blockStoreCache;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>An interface that provides information about the chain and validation.</summary>
        private readonly IChainState chainState;

        /// <summary>
        /// Current network for the active controller instance.
        /// </summary>
        private readonly Network network;

        private readonly IBlockRepository blockRepository;

        private readonly ConcurrentChain chain;

        public TransactionStoreController(
            Network network,
            ILoggerFactory loggerFactory,
            IBlockStore blockStoreCache,
            ConcurrentChain chain,
            IBlockRepository blockRepository,
            IChainState chainState)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(chainState, nameof(chainState));

            this.network = network;
            this.blockStoreCache = blockStoreCache;
            this.chain = chain;
            this.chainState = chainState;
            this.blockRepository = blockRepository;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <summary>
        /// Retrieves a given block given a block hash.
        /// </summary>
        /// <param name="query">A <see cref="SearchByHashRequest"/> model with a specific hash.</param>
        /// <returns><see cref="BlockModel"/> if block is found, <see cref="NotFoundObjectResult"/> if not found. Returns <see cref="IActionResult"/> with error information if exception thrown.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(PosBlockModel[]), 200)]
        public async Task<IActionResult> GetTransactionsAsync()
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            var pageSize = 10; // Should we allow page size to be set in query?
            //this.logger.LogTrace("(Hash:'{1}')", hash);

            try
            {
                ChainedHeader chainHeader = this.chain.Tip;

                var transactions = new List<TransactionVerboseModel>();

                while (chainHeader != null && transactions.Count < pageSize)
                {
                    Block block = await this.blockStoreCache.GetBlockAsync(chainHeader.HashBlock).ConfigureAwait(false);

                    var blockModel = new PosBlockModel(block, this.chain);

                    foreach (Transaction trx in block.Transactions)
                    {
                        // Since we got Chainheader and Tip available, we'll supply those in this query. That means this query will
                        // return more metadata than specific query using transaction ID.
                        transactions.Add(new TransactionVerboseModel(trx, this.network, chainHeader, this.chainState.BlockStoreTip));
                    }

                    chainHeader = chainHeader.Previous;
                }

                return Json(transactions);
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
        [ProducesResponseType(typeof(TransactionVerboseModel), 200)]
        [ProducesResponseType(typeof(void), 404)]
        public async Task<IActionResult> GetTransactionAsync(string id)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id", "id must be specified");
            }

            try
            {
                Transaction trx = await this.blockRepository.GetTransactionByIdAsync(new uint256(id));
                var model = new TransactionVerboseModel(trx, this.network);
                return Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}