using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using City.Features.BlockExplorer.Models;
using City.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.BlockStore.Models;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
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
        private readonly IWalletManager walletManager;

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

        private readonly IBroadcasterManager broadcasterManager;

        private readonly IConnectionManager connectionManager;

        public TransactionStoreController(
            Network network,
            IWalletManager walletManager,
            ILoggerFactory loggerFactory,
            IBlockStore blockStoreCache,
            ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IBlockRepository blockRepository,
            IConnectionManager connectionManager,
            IChainState chainState)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(blockStoreCache, nameof(blockStoreCache));
            Guard.NotNull(chainState, nameof(chainState));

            this.network = network;
            this.walletManager = walletManager;
            this.blockStoreCache = blockStoreCache;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.chainState = chainState;
            this.blockRepository = blockRepository;
            this.broadcasterManager = broadcasterManager;
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

        ///// <summary>
        ///// Sends a transaction.
        ///// </summary>
        ///// <param name="request">The hex representing the transaction.</param>
        ///// <returns></returns>
        //[Route("retry-transaction")]
        //[HttpPost]
        //public IActionResult RetryTransaction([FromBody] BroadcastTransactionModel request)
        //{
        //    //Guard.NotNull(request, nameof(request));

        //    //// checks the request is valid
        //    //if (!this.ModelState.IsValid)
        //    //{
        //    //    return ModelStateErrors.BuildErrorResponse(this.ModelState);
        //    //}

        //    //if (!this.connectionManager.ConnectedPeers.Any())
        //    //{
        //    //    this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
        //    //    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
        //    //}

        //    //try
        //    //{
        //    //    IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(request.WalletName, request.AccountName);

        //    //    Transaction transaction = this.network.CreateTransaction(request.Hex);

        //    //    var model = new WalletSendTransactionModel
        //    //    {
        //    //        TransactionId = transaction.GetHash(),
        //    //        Outputs = new List<TransactionOutputModel>()
        //    //    };

        //    //    foreach (TxOut output in transaction.Outputs)
        //    //    {
        //    //        bool isUnspendable = output.ScriptPubKey.IsUnspendable;
        //    //        model.Outputs.Add(new TransactionOutputModel
        //    //        {
        //    //            Address = isUnspendable ? null : output.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
        //    //            Amount = output.Value,
        //    //            OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
        //    //        });
        //    //    }

        //    //    this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

        //    //    TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

        //    //    if (!string.IsNullOrEmpty(transactionBroadCastEntry?.ErrorMessage))
        //    //    {
        //    //        this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
        //    //        return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
        //    //    }

        //    //    return this.Json(model);
        //    //}
        //    //catch (Exception e)
        //    //{
        //    //    this.logger.LogError("Exception occurred: {0}", e.ToString());
        //    //    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
        //    //}
        //}
    }
}