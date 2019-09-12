using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WalletController : Controller
    {
        public const int MaxHistoryItemsPerAccount = 1000;

        private readonly IWalletManager walletManager;

        private readonly IWalletTransactionHandler walletTransactionHandler;

        private readonly IWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ChainIndexer chainIndexer;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Provider of date time functionality.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        public WalletController(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chainIndexer = chainIndexer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Generates a mnemonic to use for an HD wallet.
        /// </summary>
        /// <param name="language">The language for the words in the mnemonic. The options are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional. Defaults to English.</param>
        /// <param name="wordCount">The number of words in the mnemonic. The options are: 12,15,18,21 or 24. Defaults to 12.</param>
        /// <returns>A JSON object containing the generated mnemonic.</returns>
        [Route("mnemonic")]
        [HttpGet]
        public IActionResult GenerateMnemonic([FromQuery] string language = "English", int wordCount = 12)
        {
            try
            {
                Wordlist wordList;
                switch (language.ToLowerInvariant())
                {
                    case "english":
                        wordList = Wordlist.English;
                        break;

                    case "french":
                        wordList = Wordlist.French;
                        break;

                    case "spanish":
                        wordList = Wordlist.Spanish;
                        break;

                    case "japanese":
                        wordList = Wordlist.Japanese;
                        break;

                    case "chinesetraditional":
                        wordList = Wordlist.ChineseTraditional;
                        break;

                    case "chinesesimplified":
                        wordList = Wordlist.ChineseSimplified;
                        break;

                    default:
                        throw new FormatException($"Invalid language '{language}'. Choices are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional.");
                }

                var count = (WordCount)wordCount;

                // generate the mnemonic
                var mnemonic = new Mnemonic(wordList, count);
                return this.Json(mnemonic.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Creates a new wallet on this full node.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to create a wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public IActionResult Create([FromBody]WalletCreationRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Mnemonic requestMnemonic = string.IsNullOrEmpty(request.Mnemonic) ? null : new Mnemonic(request.Mnemonic);

                Mnemonic mnemonic = this.walletManager.CreateWallet(request.Password, request.Name, request.Passphrase, mnemonic: requestMnemonic);

                // start syncing the wallet from the creation date
                this.walletSyncManager.SyncFromDate(this.dateTimeProvider.GetUtcNow());

                return this.Json(mnemonic.ToString());
            }
            catch (WalletException e)
            {
                // indicates that this wallet already exists
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (NotSupportedException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "There was a problem creating a wallet.", e.ToString());
            }
        }

        /// <summary>
        /// Signs a message and returns the signature.
        /// </summary>
        /// <param name="request">The object containing the parameters used to sign a message.</param>
        /// <returns>A JSON object containing the generated signature.</returns>
        [Route("signmessage")]
        [HttpPost]
        public IActionResult SignMessage([FromBody]SignMessageRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                string signature = this.walletManager.SignMessage(request.Password, request.WalletName, request.ExternalAddress, request.Message);
                return this.Json(signature);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Verifies the signature of a message.
        /// </summary>
        /// <param name="request">The object containing the parameters verify a signature.</param>
        /// <returns>A JSON object containing the result of the verification.</returns>
        [Route("verifymessage")]
        [HttpPost]
        public IActionResult VerifyMessage([FromBody]VerifyRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                bool result = this.walletManager.VerifySignedMessage(request.ExternalAddress, request.Message, request.Signature);
                return this.Json(result.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Loads a previously created wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to load an existing wallet</param>
        [Route("load")]
        [HttpPost]
        public IActionResult Load([FromBody]WalletLoadRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.LoadWallet(request.Password, request.Name);
                return this.Ok();
            }
            catch (FileNotFoundException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "This wallet was not found at the specified location.", e.ToString());
            }
            catch (SecurityException e)
            {
                // indicates that the password is wrong
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Wrong password, please try again.", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Recovers an existing wallet.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet.</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover")]
        [HttpPost]
        public IActionResult Recover([FromBody]WalletRecoveryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate, passphrase: request.Passphrase);

                this.SyncFromBestHeightForRecoveredWallets(request.CreationDate);

                return this.Ok();
            }
            catch (WalletException e)
            {
                // indicates that this wallet already exists
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (FileNotFoundException e)
            {
                // indicates that this wallet does not exist
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Recovers a wallet using its extended public key. Note that the recovered wallet will not have a private key and is
        /// only suitable for returning the wallet history using further API calls.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet using its extended public key.</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover-via-extpubkey")]
        [HttpPost]
        public IActionResult RecoverViaExtPubKey([FromBody]WalletExtPubRecoveryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                string accountExtPubKey =
                    this.network.IsBitcoin()
                        ? request.ExtPubKey
                        : LegacyExtPubKeyConverter.ConvertIfInLegacyStratisFormat(request.ExtPubKey, this.network);

                this.walletManager.RecoverWallet(request.Name, ExtPubKey.Parse(accountExtPubKey), request.AccountIndex,
                    request.CreationDate);

                this.SyncFromBestHeightForRecoveredWallets(request.CreationDate);

                return this.Ok();
            }
            catch (WalletException e)
            {
                // Wallet already exists.
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, e.Message, e.ToString());
            }
            catch (FileNotFoundException e)
            {
                // Wallet does not exist.
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets some general information about a wallet. This includes the network the wallet is for,
        /// the creation date and time for the wallet, the height of the blocks the wallet currently holds,
        /// and the number of connected nodes. 
        /// </summary>
        /// <param name="request">The name of the wallet to get the information for.</param>
        /// <returns>A JSON object containing the wallet information.</returns>
        [Route("general-info")]
        [HttpGet]
        public IActionResult GetGeneralInfo([FromQuery] WalletName request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.GetWallet(request.Name);

                var model = new WalletGeneralInfoModel
                {
                    Network = wallet.Network,
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.AccountsRoot.Single().LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chainIndexer.Tip.Height,
                    IsChainSynced = this.chainIndexer.IsDownloaded(),
                    IsDecrypted = true
                };

                // Get the wallet's file path.
                (string folder, IEnumerable<string> fileNameCollection) = this.walletManager.GetWalletsFiles();
                string searchFile = Path.ChangeExtension(request.Name, this.walletManager.GetWalletFileExtension());
                string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(fileName))
                {
                    model.WalletFilePath = Path.Combine(folder, fileName);
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception occurred: {0}");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the history of a wallet. This includes the transactions held by the entire wallet
        /// or a single account if one is specified. 
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's history.</param>
        /// <returns>A JSON object containing the wallet history.</returns>
        [Route("history")]
        [HttpGet]
        public IActionResult GetHistory([FromQuery] WalletHistoryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(request.WalletName, request.AccountName);

                foreach (AccountHistory accountHistory in accountsHistory)
                {
                    var transactionItems = new List<TransactionItemModel>();
                    var uniqueProcessedTxIds = new HashSet<uint256>();

                    IEnumerable<FlatHistory> query = accountHistory.History;

                    if (!string.IsNullOrEmpty(request.Address))
                    {
                        query = query.Where(x => x.Address.Address == request.Address);
                    }

                    // Sorting the history items by descending dates. That includes received and sent dates.
                    List<FlatHistory> items = query
                                                .OrderBy(o => o.Transaction.IsConfirmed() ? 1 : 0)
                                                .ThenByDescending(o => o.Transaction.SpendingDetails?.CreationTime ?? o.Transaction.CreationTime)
                                                .ToList();

                    // Represents a sublist containing only the transactions that have already been spent.
                    List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                    // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                    // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                    List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && t.Transaction.IsSpent())).ToList();

                    // Represents a sublist of 'change' transactions.
                    List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                    foreach (FlatHistory item in history)
                    {
                        // Count only unique transactions and limit it to MaxHistoryItemsPerAccount.
                        int processedTransactions = uniqueProcessedTxIds.Count;
                        if (processedTransactions >= MaxHistoryItemsPerAccount)
                        {
                            break;
                        }

                        TransactionData transaction = item.Transaction;
                        HdAddress address = item.Address;

                        // First we look for staking transaction as they require special attention.
                        // A staking transaction spends one of our inputs into 2 outputs or more, paid to the same address.
                        if (transaction.SpendingDetails?.IsCoinStake != null && transaction.SpendingDetails.IsCoinStake.Value)
                        {
                            // We look for the output(s) related to our spending input.
                            List<FlatHistory> relatedOutputs = items.Where(h => h.Transaction.Id == transaction.SpendingDetails.TransactionId && h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList();
                            if (relatedOutputs.Any())
                            {
                                // Add staking transaction details.
                                // The staked amount is calculated as the difference between the sum of the outputs and the input and should normally be equal to 1.
                                var stakingItem = new TransactionItemModel
                                {
                                    Type = TransactionItemType.Staked,
                                    ToAddress = address.Address,
                                    Amount = relatedOutputs.Sum(o => o.Transaction.Amount) - transaction.Amount,
                                    Id = transaction.SpendingDetails.TransactionId,
                                    Timestamp = transaction.SpendingDetails.CreationTime,
                                    ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                    BlockIndex = transaction.SpendingDetails.BlockIndex
                                };

                                transactionItems.Add(stakingItem);
                                uniqueProcessedTxIds.Add(stakingItem.Id);
                            }

                            // No need for further processing if the transaction itself is the output of a staking transaction.
                            if (transaction.IsCoinStake != null)
                            {
                                continue;
                            }
                        }

                        // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                        if (transaction.SpendingDetails != null && transaction.SpendingDetails.IsCoinStake == null)
                        {
                            // Create a record for a 'send' transaction.
                            uint256 spendingTransactionId = transaction.SpendingDetails.TransactionId;
                            var sentItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Send,
                                Id = spendingTransactionId,
                                Timestamp = transaction.SpendingDetails.CreationTime,
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                BlockIndex = transaction.SpendingDetails.BlockIndex,
                                Amount = Money.Zero
                            };

                            // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
                            if (transaction.SpendingDetails.Payments != null)
                            {
                                sentItem.Payments = new List<PaymentDetailModel>();
                                foreach (PaymentDetails payment in transaction.SpendingDetails.Payments)
                                {
                                    sentItem.Payments.Add(new PaymentDetailModel
                                    {
                                        DestinationAddress = payment.DestinationAddress,
                                        Amount = payment.Amount
                                    });

                                    sentItem.Amount += payment.Amount;
                                }
                            }

                            // Get the change address for this spending transaction.
                            FlatHistory changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                            // Find all the spending details containing the spending transaction id and aggregate the sums.
                            // This is our best shot at finding the total value of inputs for this transaction.
                            var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                            // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                            sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                            // Mined/staked coins add more coins to the total out.
                            // That makes the fee negative. If that's the case ignore the fee.
                            if (sentItem.Fee < 0)
                                sentItem.Fee = 0;

                            transactionItems.Add(sentItem);
                            uniqueProcessedTxIds.Add(sentItem.Id);
                        }

                        // We don't show in history transactions that are outputs of staking transactions.
                        if (transaction.IsCoinStake != null && transaction.IsCoinStake.Value && transaction.SpendingDetails == null)
                        {
                            continue;
                        }

                        // Create a record for a 'receive' transaction.
                        if (transaction.IsCoinStake == null && !address.IsChangeAddress())
                        {
                            // First check if we already have a similar transaction output, in which case we just sum up the amounts
                            TransactionItemModel existingReceivedItem = this.FindSimilarReceivedTransactionOutput(transactionItems, transaction);

                            if (existingReceivedItem == null)
                            {
                                // Add incoming fund transaction details.
                                var receivedItem = new TransactionItemModel
                                {
                                    Type = TransactionItemType.Received,
                                    ToAddress = address.Address,
                                    Amount = transaction.Amount,
                                    Id = transaction.Id,
                                    Timestamp = transaction.CreationTime,
                                    ConfirmedInBlock = transaction.BlockHeight,
                                    BlockIndex = transaction.BlockIndex
                                };

                                transactionItems.Add(receivedItem);
                                uniqueProcessedTxIds.Add(receivedItem.Id);
                            }
                            else
                            {
                                existingReceivedItem.Amount += transaction.Amount;
                            }
                        }
                    }

                    transactionItems = transactionItems.Distinct(new SentTransactionItemModelComparer()).Select(e => e).ToList();

                    // Sort and filter the history items.
                    List<TransactionItemModel> itemsToInclude = transactionItems.OrderByDescending(t => t.Timestamp)
                        .Where(x => string.IsNullOrEmpty(request.SearchQuery) || (x.Id.ToString() == request.SearchQuery || x.ToAddress == request.SearchQuery || x.Payments.Any(p => p.DestinationAddress == request.SearchQuery)))
                        .Skip(request.Skip ?? 0)
                        .Take(request.Take ?? transactionItems.Count)
                        .ToList();

                    model.AccountsHistoryModel.Add(new AccountHistoryModel
                    {
                        TransactionsHistory = itemsToInclude,
                        Name = accountHistory.Account.Name,
                        CoinType = this.coinType,
                        HdPath = accountHistory.Account.HdPath
                    });
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance of a wallet in STRAT (or sidechain coin). Both the confirmed and unconfirmed balance are returned.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's balance.</param>
        /// <returns>A JSON object containing the wallet balance.</returns>
        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery] WalletBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var model = new WalletBalanceModel();

                IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(request.WalletName, request.AccountName);

                foreach (AccountBalance balance in balances)
                {
                    HdAccount account = balance.Account;
                    model.AccountsBalances.Add(new AccountBalanceModel
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = balance.AmountConfirmed,
                        AmountUnconfirmed = balance.AmountUnconfirmed,
                        SpendableAmount = balance.SpendableAmount,
                        Addresses = account.GetCombinedAddresses().Select(address =>
                        {
                            (Money confirmedAmount, Money unConfirmedAmount) = address.GetBalances();
                            return new AddressModel
                            {
                                Address = address.Address,
                                IsUsed = address.Transactions.Any(),
                                IsChange = address.IsChangeAddress(),
                                AmountConfirmed = confirmedAmount,
                                AmountUnconfirmed = unConfirmedAmount
                            };
                        })
                    });
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance at a specific wallet address in STRAT (or sidechain coin).
        /// Both the confirmed and unconfirmed balance are returned.
        /// This method gets the UTXOs at the address which the wallet can spend.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the balance
        /// at a specific wallet address.</param>
        /// <returns>A JSON object containing the balance, fee, and an address for the balance.</returns>
        [Route("received-by-address")]
        [HttpGet]
        public IActionResult GetReceivedByAddress([FromQuery] ReceivedByAddressRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                AddressBalance balanceResult = this.walletManager.GetAddressBalance(request.Address);
                return this.Json(new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed
                });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the maximum spendable balance for an account along with the fee required to spend it.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the
        /// maximum spendable balance on an account.</param>
        /// <returns>A JSON object containing the maximum spendable balance for an account
        /// along with the fee required to spend it.</returns>
        [Route("maxbalance")]
        [HttpGet]
        public IActionResult GetMaximumSpendableBalance([FromQuery] WalletMaximumBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                (Money maximumSpendableAmount, Money Fee) transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(request.WalletName, request.AccountName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
                return this.Json(new MaxSpendableAmountModel
                {
                    MaxSpendableAmount = transactionResult.maximumSpendableAmount,
                    Fee = transactionResult.Fee
                });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the spendable transactions for an account with the option to specify how many confirmations
        /// a transaction needs to be included.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the spendable 
        /// transactions for an account.</param>
        /// <returns>A JSON object containing the spendable transactions for an account.</returns>
        [Route("spendable-transactions")]
        [HttpGet]
        public IActionResult GetSpendableTransactions([FromQuery] SpendableTransactionsRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                IEnumerable<UnspentOutputReference> spendableTransactions = this.walletManager.GetSpendableTransactionsInAccount(new WalletAccountReference(request.WalletName, request.AccountName), request.MinConfirmations);

                return this.Json(new SpendableTransactionsModel
                {
                    SpendableTransactions = spendableTransactions.Select(st => new SpendableTransactionModel
                    {
                        Id = st.Transaction.Id,
                        Amount = st.Transaction.Amount,
                        Address = st.Address.Address,
                        Index = st.Transaction.Index,
                        IsChange = st.Address.IsChangeAddress(),
                        CreationTime = st.Transaction.CreationTime,
                        Confirmations = st.Confirmations
                    }).ToList()
                });
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a fee estimate for a specific transaction.
        /// Fee can be estimated by creating a <see cref="TransactionBuildContext"/> with no password
        /// and then building the transaction and retrieving the fee from the context.
        /// </summary>
        /// <param name="request">An object containing the parameters used to estimate the fee 
        /// for a specific transaction.</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-txfee")]
        [HttpPost]
        public IActionResult GetTransactionFeeEstimate([FromBody]TxFeeEstimateRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var recipients = new List<Recipient>();
                foreach (RecipientModel recipientModel in request.Recipients)
                {
                    recipients.Add(new Recipient
                    {
                        ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                        Amount = recipientModel.Amount
                    });
                }

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Recipients = recipients,
                    OpReturnData = request.OpReturnData,
                    OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                    Sign = false
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
        /// Builds a transaction and returns the hex to use when executing the transaction.
        /// </summary>
        /// <param name="request">An object containing the parameters used to build a transaction.</param>
        /// <returns>A JSON object including the transaction ID, the hex used to execute
        /// the transaction, and the transaction fee.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public IActionResult BuildTransaction([FromBody] BuildTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var recipients = new List<Recipient>();
                foreach (RecipientModel recipientModel in request.Recipients)
                {
                    recipients.Add(new Recipient
                    {
                        ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                        Amount = recipientModel.Amount
                    });
                }

                // If specified, get the change address, which must already exist in the wallet.
                HdAddress changeAddress = null;
                if (!string.IsNullOrWhiteSpace(request.ChangeAddress))
                {
                    Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                    HdAccount account = wallet.GetAccount(request.AccountName);
                    if (account == null)
                    {
                        return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Account not found.", $"No account with the name '{request.AccountName}' could be found in wallet {wallet.Name}.");
                    }

                    changeAddress = account.GetCombinedAddresses().FirstOrDefault(x => x.Address == request.ChangeAddress);

                    if (changeAddress == null)
                    {
                        return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Change address not found.", $"No changed address '{request.ChangeAddress}' could be found in wallet {wallet.Name}.");
                    }
                }

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                    TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = request.ShuffleOutputs ?? true, // We shuffle transaction outputs by default as it's better for anonymity.
                    OpReturnData = request.OpReturnData,
                    OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                    WalletPassword = request.Password,
                    SelectedInputs = request.Outpoints?.Select(u => new OutPoint(uint256.Parse(u.TransactionId), u.Index)).ToList(),
                    AllowOtherInputs = false,
                    Recipients = recipients,
                    ChangeAddress = changeAddress
                };

                if (!string.IsNullOrEmpty(request.FeeType))
                {
                    context.FeeType = FeeParser.Parse(request.FeeType);
                }

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

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
        /// Sends a transaction that has already been built.
        /// Use the /api/Wallet/build-transaction call to create transactions.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters used to a send transaction request.</param>
        /// <returns>A JSON object containing information about the sent transaction.</returns>
        [Route("send-transaction")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            if (!this.connectionManager.ConnectedPeers.Any())
            {
                this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
            }

            try
            {
                Transaction transaction = this.network.CreateTransaction(request.Hex);

                var model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (TxOut output in transaction.Outputs)
                {
                    bool isUnspendable = output.ScriptPubKey.IsUnspendable;
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = isUnspendable ? null : output.ScriptPubKey.GetDestinationAddress(this.network)?.ToString(),
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

                if (transactionBroadCastEntry.State == State.CantBroadcast)
                {
                    this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists all the files found in the default wallet folder.
        /// </summary>
        /// <returns>A JSON object containing the wallet folder path and
        /// the names of the files found within the folder.</returns>
        [Route("files")]
        [HttpGet]
        public IActionResult ListWalletsFiles()
        {
            try
            {
                (string folderPath, IEnumerable<string> filesNames) result = this.walletManager.GetWalletsFiles();
                var model = new WalletFileModel
                {
                    WalletsPath = result.folderPath,
                    WalletsFiles = result.filesNames
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
        /// Creates a new account for a wallet.
        /// Accounts are given the name "account i", where i is an incremental index which starts at 0.
        /// According to BIP44. an account at index i can only be created when the account at index (i - 1)
        /// contains at least one transaction. For example, if three accounts named "account 0", "account 1",
        /// and "account 2" already exist and contain at least one transaction, then the
        /// the function will create "account 3". However, if "account 2", for example, instead contains no
        /// transactions, then this API call returns "account 2".
        /// Accounts are created deterministically, which means that on any device, the accounts and addresses
        /// for a given seed (or mnemonic) are always the same.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to create a new account in a wallet.</param>
        /// <returns>A JSON object containing the name of the new account or an existing account
        /// containing no transactions.</returns>
        [Route("account")]
        [HttpPost]
        public IActionResult CreateNewAccount([FromBody]GetUnusedAccountModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                HdAccount result = this.walletManager.GetUnusedAccount(request.WalletName, request.Password);
                return this.Json(result.Name);
            }
            catch (CannotAddAccountToXpubKeyWalletException e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, e.Message, string.Empty);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a list of accounts for the specified wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to list the accounts for a wallet.</param>
        /// <returns>A JSON object containing a list of accounts for the specified wallet.</returns>
        [Route("accounts")]
        [HttpGet]
        public IActionResult ListAccounts([FromQuery]ListAccountsModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                IEnumerable<HdAccount> result = this.walletManager.GetAccounts(request.WalletName);
                return this.Json(result.Select(a => a.Name));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets an unused address (in the Base58 format) for a wallet account. This address will not have been assigned
        /// to any known UTXO (neither to pay funds into the wallet or to pay change back to the wallet).
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to retrieve an
        /// unused address for a wallet account.</param>
        /// <returns>A JSON object containing the last created and unused address (in Base58 format).</returns>
        [Route("unusedaddress")]
        [HttpGet]
        public IActionResult GetUnusedAddress([FromQuery]GetUnusedAddressModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                HdAddress result = this.walletManager.GetUnusedAddress(new WalletAccountReference(request.WalletName, request.AccountName));
                return this.Json(result.Address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a specified number of unused addresses (in the Base58 format) for a wallet account. These addresses
        /// will not have been assigned to any known UTXO (neither to pay funds into the wallet or to pay change back
        /// to the wallet).
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// unused addresses for a wallet account.</param>
        /// <returns>A JSON object containing the required amount of unused addresses (in Base58 format).</returns>
        /// </summary>
        [Route("unusedaddresses")]
        [HttpGet]
        public IActionResult GetUnusedAddresses([FromQuery]GetUnusedAddressesModel request)
        {
            Guard.NotNull(request, nameof(request));
            int count = int.Parse(request.Count);

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                IEnumerable<HdAddress> result = this.walletManager.GetUnusedAddresses(new WalletAccountReference(request.WalletName, request.AccountName), count);
                return this.Json(result.Select(x => x.Address).ToArray());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets all addresses for a wallet account.
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// all addresses for a wallet account.</param>
        /// <returns>A JSON object containing all addresses for a wallet account (in Base58 format).</returns>
        /// </summary>
        [Route("addresses")]
        [HttpGet]
        public IActionResult GetAllAddresses([FromQuery]GetAllAddressesModel request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccount(request.AccountName);
                if (account == null)
                    throw new WalletException($"No account with the name '{request.AccountName}' could be found.");

                var model = new AddressesModel
                {
                    Addresses = account.GetCombinedAddresses().Select(address =>
                    {
                        (Money confirmedAmount, Money unConfirmedAmount) = address.GetBalances();

                        return new AddressModel
                        {
                            Address = address.Address,
                            IsUsed = address.Transactions.Any(),
                            IsChange = address.IsChangeAddress(),
                            AmountConfirmed = confirmedAmount,
                            AmountUnconfirmed = unConfirmedAmount
                        };
                    })
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
        /// Removes transactions from the wallet.
        /// You might want to remove transactions from a wallet if some unconfirmed transactions disappear
        /// from the blockchain or the transaction fields within the wallet are updated and a refresh is required to
        /// populate the new fields. 
        /// In one situation, you might notice several unconfirmed transaction in the wallet, which you now know were
        /// never confirmed. You can use this API to correct this by specifying a date and time before the first
        /// unconfirmed transaction thereby removing all transactions after this point. You can also request a resync as
        /// part of the call, which calculates the block height for the earliest removal. The wallet sync manager then
        /// proceeds to resync from there reinstating the confirmed transactions in the wallet. You can also cherry pick
        /// transactions to remove by specifying their transaction ID. 
        /// 
        /// <param name="request">An object containing the necessary parameters to remove transactions
        /// from a wallet. The includes several options for specifying the transactions to remove.</param>
        /// <returns>A JSON object containing all removed transactions identified by their
        /// transaction ID and creation time.</returns>
        /// </summary>
        [Route("remove-transactions")]
        [HttpDelete]
        public IActionResult RemoveTransactions([FromQuery]RemoveTransactionsModel request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = this.walletManager.RemoveAllTransactions(request.WalletName);
                }
                else if (request.FromDate != default(DateTime))
                {
                    result = this.walletManager.RemoveTransactionsFromDate(request.WalletName, request.FromDate);
                }
                else if (request.TransactionsIds != null)
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = this.walletManager.RemoveTransactionsByIds(request.WalletName, ids);
                }
                else
                {
                    throw new WalletException("A filter specifying what transactions to remove must be set.");
                }

                // If the user chose to resync the wallet after removing transactions.
                if (result.Any() && request.ReSync)
                {
                    // From the list of removed transactions, check which one is the oldest and retrieve the block right before that time.
                    DateTimeOffset earliestDate = result.Min(r => r.creationTime);
                    ChainedHeader chainedHeader = this.chainIndexer.GetHeader(this.chainIndexer.GetHeightAtTime(earliestDate.DateTime));

                    // Update the wallet and save it to the file system.
                    Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                    wallet.SetLastBlockDetails(chainedHeader);
                    this.walletManager.SaveWallet(wallet);

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1);
                }

                IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the extended public key of a specified wallet account.
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// the extended public key for a wallet account.</param>
        /// <returns>A JSON object containing the extended public key for a wallet account.</returns>
        /// </summary>
        [Route("extpubkey")]
        [HttpGet]
        public IActionResult GetExtPubKey([FromQuery]GetExtPubKeyModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                string result = this.walletManager.GetExtPubKey(new WalletAccountReference(request.WalletName, request.AccountName));
                return this.Json(result);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Requests the node resyncs from a block specified by its block hash.
        /// Internally, the specified block is taken as the new wallet tip
        /// and all blocks after it are resynced.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters
        /// to request a resync.</param>
        /// <returns>A value of Ok if the resync was successful.</returns>
        [HttpPost]
        [Route("sync")]
        public IActionResult Sync([FromBody] HashModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            ChainedHeader block = this.chainIndexer.GetHeader(uint256.Parse(model.Hash));

            if (block == null)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {model.Hash} was not found on the blockchain.", string.Empty);
            }

            this.walletSyncManager.SyncFromHeight(block.Height);
            return this.Ok();
        }

        /// <summary>
        /// Request the node resyncs starting from a given date and time.
        /// Internally, the first block created on or after the supplied date and time
        /// is taken as the new wallet tip and all blocks after it are resynced.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters
        /// to request a resync.</param>
        /// <returns>A value of Ok if the resync was successful.</returns>
        [HttpPost]
        [Route("sync-from-date")]
        public IActionResult SyncFromDate([FromBody] WalletSyncFromDateRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            this.walletSyncManager.SyncFromDate(request.Date);

            return this.Ok();
        }

        /// <summary>Creates requested amount of UTXOs each of equal value.</summary>
        [HttpPost]
        [Route("splitcoins")]
        public IActionResult SplitCoins([FromBody] SplitCoinsRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var walletReference = new WalletAccountReference(request.WalletName, request.AccountName);
                HdAddress address = this.walletManager.GetUnusedAddress(walletReference);

                Money totalAmount = request.TotalAmountToSplit;
                Money singleUtxoAmount = totalAmount / request.UtxosCount;

                var recipients = new List<Recipient>(request.UtxosCount);
                for (int i = 0; i < request.UtxosCount; i++)
                    recipients.Add(new Recipient { ScriptPubKey = address.ScriptPubKey, Amount = singleUtxoAmount });

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = walletReference,
                    MinConfirmations = 1,
                    Shuffle = true,
                    WalletPassword = request.WalletPassword,
                    Recipients = recipients,
                    Time = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()
                };

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                return this.SendTransaction(new SendTransactionRequest(transactionResult.ToHex()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        private void SyncFromBestHeightForRecoveredWallets(DateTime walletCreationDate)
        {
            // After recovery the wallet needs to be synced.
            // We only sync if the syncing process needs to go back.
            int blockHeightToSyncFrom = this.chainIndexer.GetHeightAtTime(walletCreationDate);
            int currentSyncingHeight = this.walletSyncManager.WalletTip.Height;

            if (blockHeightToSyncFrom < currentSyncingHeight)
            {
                this.walletSyncManager.SyncFromHeight(blockHeightToSyncFrom);
            }
        }

        private TransactionItemModel FindSimilarReceivedTransactionOutput(List<TransactionItemModel> items, TransactionData transaction)
        {
            TransactionItemModel existingTransaction = items.FirstOrDefault(i => i.Id == transaction.Id &&
                                                                                 i.Type == TransactionItemType.Received &&
                                                                                 i.ConfirmedInBlock == transaction.BlockHeight);
            return existingTransaction;
        }
    }
}
