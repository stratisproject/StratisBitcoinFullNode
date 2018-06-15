using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using Recipient = Stratis.Bitcoin.Features.Wallet.Recipient;
using TransactionBuildContext = Stratis.Bitcoin.Features.Wallet.TransactionBuildContext;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationWalletController : Controller
    {
        private readonly IFederationWalletManager walletManager;

        private readonly IFederationWalletTransactionHandler walletTransactionHandler;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public FederationWalletController(
            ILoggerFactory loggerFactory,
            IFederationWalletManager walletManager,
            IFederationWalletTransactionHandler walletTransactionHandler,
            IFederationWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Get some general info about a wallet.
        /// </summary>
        /// <param name="request">The name of the wallet.</param>
        /// <returns></returns>
        [Route("general-info")]
        [HttpGet]
        public IActionResult GetGeneralInfo()
        {
            try
            {
                FederationWallet wallet = this.walletManager.GetWallet();

                if (wallet == null)
                {
                    return this.NotFound("No federation wallet found.");
                }

                var model = new WalletGeneralInfoModel
                {
                    Network = wallet.Network,
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chain.Tip.Height,
                    IsChainSynced = this.chain.IsDownloaded(),
                    IsDecrypted = true
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves the history of a wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        //[Route("history")]
        //[HttpGet]
        //public IActionResult GetHistory([FromQuery] WalletHistoryRequest request)
        //{
        //    Guard.NotNull(request, nameof(request));

        //    // Checks the request is valid.
        //    if (!this.ModelState.IsValid)
        //    {
        //        return BuildErrorResponse(this.ModelState);
        //    }

        //    try
        //    {
        //        var model = new WalletHistoryModel();

        //        // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
        //        IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(request.WalletName, request.AccountName);

        //        foreach (AccountHistory accountHistory in accountsHistory)
        //        {
        //            var transactionItems = new List<TransactionItemModel>();

        //            List<FlatHistory> items = accountHistory.History.OrderByDescending(o => o.Transaction.CreationTime).Take(200).ToList();

        //            // Represents a sublist containing only the transactions that have already been spent.
        //            List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

        //            // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
        //            // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
        //            List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();

        //            // Represents a sublist of 'change' transactions.
        //            List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

        //            foreach (FlatHistory item in history)
        //            {
        //                TransactionData transaction = item.Transaction;
        //                HdAddress address = item.Address;

        //                // We don't show in history transactions that are outputs of staking transactions.
        //                if (transaction.IsCoinStake != null && transaction.IsCoinStake.Value && transaction.SpendingDetails == null)
        //                {
        //                    continue;
        //                }

        //                // First we look for staking transaction as they require special attention.
        //                // A staking transaction spends one of our inputs into 2 outputs, paid to the same address.
        //                if (transaction.SpendingDetails?.IsCoinStake != null && transaction.SpendingDetails.IsCoinStake.Value)
        //                {
        //                    // We look for the 2 outputs related to our spending input.
        //                    List<FlatHistory> relatedOutputs = items.Where(h => h.Transaction.Id == transaction.SpendingDetails.TransactionId && h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList();
        //                    if (relatedOutputs.Any())
        //                    {
        //                        // Add staking transaction details.
        //                        // The staked amount is calculated as the difference between the sum of the outputs and the input and should normally be equal to 1.
        //                        var stakingItem = new TransactionItemModel
        //                        {
        //                            Type = TransactionItemType.Staked,
        //                            ToAddress = address.Address,
        //                            Amount = relatedOutputs.Sum(o => o.Transaction.Amount) - transaction.Amount,
        //                            Id = transaction.SpendingDetails.TransactionId,
        //                            Timestamp = transaction.SpendingDetails.CreationTime,
        //                            ConfirmedInBlock = transaction.SpendingDetails.BlockHeight
        //                        };

        //                        transactionItems.Add(stakingItem);
        //                    }

        //                    // No need for further processing if the transaction itself is the output of a staking transaction.
        //                    if (transaction.IsCoinStake != null)
        //                    {
        //                        continue;
        //                    }
        //                }

        //                // Create a record for a 'receive' transaction.
        //                if (!address.IsChangeAddress())
        //                {
        //                    // Add incoming fund transaction details.
        //                    var receivedItem = new TransactionItemModel
        //                    {
        //                        Type = TransactionItemType.Received,
        //                        ToAddress = address.Address,
        //                        Amount = transaction.Amount,
        //                        Id = transaction.Id,
        //                        Timestamp = transaction.CreationTime,
        //                        ConfirmedInBlock = transaction.BlockHeight
        //                    };

        //                    transactionItems.Add(receivedItem);
        //                }

        //                // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
        //                if (transaction.SpendingDetails != null && transaction.SpendingDetails.IsCoinStake == null)
        //                {
        //                    // Create a record for a 'send' transaction.
        //                    uint256 spendingTransactionId = transaction.SpendingDetails.TransactionId;
        //                    var sentItem = new TransactionItemModel
        //                    {
        //                        Type = TransactionItemType.Send,
        //                        Id = spendingTransactionId,
        //                        Timestamp = transaction.SpendingDetails.CreationTime,
        //                        ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
        //                        Amount = Money.Zero
        //                    };

        //                    // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
        //                    if (transaction.SpendingDetails.Payments != null)
        //                    {
        //                        sentItem.Payments = new List<PaymentDetailModel>();
        //                        foreach (PaymentDetails payment in transaction.SpendingDetails.Payments)
        //                        {
        //                            sentItem.Payments.Add(new PaymentDetailModel
        //                            {
        //                                DestinationAddress = payment.DestinationAddress,
        //                                Amount = payment.Amount
        //                            });

        //                            sentItem.Amount += payment.Amount;
        //                        }
        //                    }

        //                    // Get the change address for this spending transaction.
        //                    FlatHistory changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

        //                    // Find all the spending details containing the spending transaction id and aggregate the sums.
        //                    // This is our best shot at finding the total value of inputs for this transaction.
        //                    var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

        //                    // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
        //                    sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

        //                    // Mined/staked coins add more coins to the total out.
        //                    // That makes the fee negative. If that's the case ignore the fee.
        //                    if (sentItem.Fee < 0)
        //                        sentItem.Fee = 0;

        //                    if (!transactionItems.Contains(sentItem, new SentTransactionItemModelComparer()))
        //                    {
        //                        transactionItems.Add(sentItem);
        //                    }
        //                }
        //            }

        //            model.AccountsHistoryModel.Add(new AccountHistoryModel
        //            {
        //                TransactionsHistory = transactionItems.OrderByDescending(t => t.Timestamp).ToList(),
        //                Name = accountHistory.Account.Name,
        //                CoinType = this.coinType,
        //                HdPath = accountHistory.Account.HdPath
        //            });
        //        }

        //        return this.Json(model);
        //    }
        //    catch (Exception e)
        //    {
        //        this.logger.LogError("Exception occurred: {0}", e.ToString());
        //        return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
        //    }
        //}

        /// <summary>
        /// Gets the balance of a wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance()
        {
            try
            {
                FederationWallet wallet = this.walletManager.GetWallet();
                if (wallet == null)
                {
                    return this.NotFound("No federation wallet found.");
                }

                var result = wallet.GetSpendableAmount();

                AccountBalanceModel balance = new AccountBalanceModel
                {
                    CoinType = this.coinType,
                    AmountConfirmed = result.ConfirmedAmount,
                    AmountUnconfirmed = result.UnConfirmedAmount,
                };

                WalletBalanceModel model = new WalletBalanceModel();
                model.AccountsBalances.Add(balance);

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a transaction fee estimate.
        /// Fee can be estimated by creating a <see cref="Bitcoin.Features.Wallet.TransactionBuildContext"/> with no password
        /// and then building the transaction and retrieving the fee from the context.
        /// </summary>
        /// <param name="request">The transaction parameters.</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-txfee")]
        [HttpGet]
        public IActionResult GetTransactionFeeEstimate([FromQuery]TxFeeEstimateRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var destination = BitcoinAddress.Create(request.DestinationAddress, this.network).ScriptPubKey;
                var context = new Wallet.TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Wallet.Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList())
                {
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                };

                return this.Json(this.walletTransactionHandler.EstimateFee(context));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds a transaction.
        /// </summary>
        /// <param name="request">The transaction parameters.</param>
        /// <returns>All the details of the transaction, including the hex used to execute it.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public IActionResult BuildTransaction([FromBody] BuildTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var destination = BitcoinAddress.Create(request.DestinationAddress, this.network).ScriptPubKey;
                var context = new Wallet.TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Wallet.Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList(),
                    request.Password, request.OpReturnData)
                {
                    TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = request.ShuffleOutputs ?? true // We shuffle transaction outputs by default as it's better for anonymity.
                };

                if (!string.IsNullOrEmpty(request.FeeType))
                {
                    context.FeeType = FeeParser.Parse(request.FeeType);
                }

                var transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                var model = new WalletBuildTransactionModel
                {
                    Hex = transactionResult.ToHex(),
                    Fee = context.TransactionFee,
                    TransactionId = transactionResult.GetHash()
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Sends a transaction.
        /// </summary>
        /// <param name="request">The hex representing the transaction.</param>
        /// <returns></returns>
        [Route("send-transaction")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            if (!this.connectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                var transaction = Transaction.Load(request.Hex, this.network);

                WalletSendTransactionModel model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (var output in transaction.Outputs)
                {
                    var isUnspendable = output.ScriptPubKey.IsUnspendable;
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = isUnspendable ? null : output.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.walletManager.ProcessTransaction(transaction, null, null, false);

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
        
        /// <summary>
        /// Starts sending block to the wallet for synchronisation.
        /// This is for demo and testing use only.
        /// </summary>
        /// <param name="model">The hash of the block from which to start syncing.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("sync")]
        public IActionResult Sync([FromBody] HashModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            ChainedHeader block = this.chain.GetBlock(uint256.Parse(model.Hash));

            if (block == null)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {model.Hash} was not found on the blockchain.", string.Empty);
            }

            this.walletSyncManager.SyncFromHeight(block.Height);
            return this.Ok();
        }

        /// <summary>
        /// Imports the federation member's mnemonic key.
        /// </summary>
        /// <param name="request">The object containing the parameters used to recover a wallet.</param>
        /// <returns></returns>
        [Route("import-key")]
        [HttpPost]
        public IActionResult ImportMemberKey([FromBody]ImportMemberKeyRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                this.walletManager.ImportMemberKey(request.Password, request.Mnemonic);
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds an <see cref="IActionResult"/> containing errors contained in the <see cref="ControllerBase.ModelState"/>.
        /// </summary>
        /// <returns>A result containing the errors.</returns>
        private static IActionResult BuildErrorResponse(ModelStateDictionary modelState)
        {
            List<ModelError> errors = modelState.Values.SelectMany(e => e.Errors).ToList();
            return ErrorHelpers.BuildErrorResponse(
                HttpStatusCode.BadRequest,
                string.Join(Environment.NewLine, errors.Select(m => m.ErrorMessage)),
                string.Join(Environment.NewLine, errors.Select(m => m.Exception?.Message)));
        }
    }
}