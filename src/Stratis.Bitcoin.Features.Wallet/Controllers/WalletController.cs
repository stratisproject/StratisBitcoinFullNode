using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WalletController : Controller
    {
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IWalletSyncManager walletSyncManager;
        private readonly CoinType coinType;
        
        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly DataFolder dataFolder;
        
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
            ConcurrentChain chain,
            DataFolder dataFolder,
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
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Generates a new mnemonic. The call can optionally specify a language and the number of words in the mnemonic.
        /// </summary>        
        /// <param name="language">The language for the words in the mnemonic. Options are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional. The default is 'English'.</param>
        /// <param name="wordCount">The number of words in the mnemonic. Options are: 12,15,18,21 or 24. the default is 12.</param>
        /// <returns>A JSON object containing the mnemonic generated.</returns>
        [Route("mnemonic")]
        [HttpGet]
        public IActionResult GenerateMnemonic([FromQuery] string language = "English", int wordCount = 12)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(language), language, nameof(wordCount), wordCount);

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

                WordCount count = (WordCount)wordCount;

                // generate the mnemonic 
                Mnemonic mnemonic = new Mnemonic(wordList, count);
                return this.Json(mnemonic.ToString());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Creates a new wallet on the local machine.
        /// </summary>
        /// <param name="request">The object containing the parameters used to create the wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public IActionResult Create([FromBody]WalletCreationRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Mnemonic mnemonic = this.walletManager.CreateWallet(request.Password, request.Name, mnemonic: request.Mnemonic);

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
        /// Loads a wallet previously created by the user.
        /// </summary>
        /// <param name="request">The name of the wallet to load.</param>
        /// <returns></returns>
        [Route("load")]
        [HttpPost]
        public IActionResult Load([FromBody]WalletLoadRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
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
        /// Recovers a wallet.
        /// </summary>
        /// <param name="request">The object containing the parameters used to recover a wallet.</param>
        /// <returns></returns>
        [Route("recover")]
        [HttpPost]
        public IActionResult Recover([FromBody]WalletRecoveryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate, null);

                // start syncing the wallet from the creation date
                this.walletSyncManager.SyncFromDate(request.CreationDate);

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
        /// Get some general info about a wallet.
        /// </summary>
        /// <param name="request">The name of the wallet.</param>
        /// <returns></returns>
        [Route("general-info")]
        [HttpGet]
        public IActionResult GetGeneralInfo([FromQuery] WalletName request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet wallet = this.walletManager.GetWallet(request.Name);

                var model = new WalletGeneralInfoModel
                {
                    Network = wallet.Network,
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.AccountsRoot.Single(a => a.CoinType == this.coinType).LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedNodes.Count(),
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
        [Route("history")]
        [HttpGet]
        public IActionResult GetHistory([FromQuery] WalletHistoryRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel();

                // get transactions contained in the wallet
                var items = this.walletManager.GetHistory(request.WalletName).ToList().OrderByDescending(o => o.Transaction.CreationTime).Take(100).ToList();
                List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();
                List<FlatHistory> filtered = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();
                List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                foreach (var item in filtered)
                {
                    var transaction = item.Transaction;
                    var address = item.Address;

                    if (!address.IsChangeAddress())
                    {
                        // add incoming fund transaction details
                        TransactionItemModel receivedItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Received,
                            ToAddress = address.Address,
                            Amount = transaction.Amount,
                            Id = transaction.Id,
                            Timestamp = transaction.CreationTime,
                            ConfirmedInBlock = transaction.BlockHeight
                        };

                        model.TransactionsHistory.Add(receivedItem);
                    }

                    // add outgoing fund transaction details
                    if (transaction.SpendingDetails != null)
                    {
                        var spendingTransactionId = transaction.SpendingDetails.TransactionId;
                        TransactionItemModel sentItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Send,
                            Id = spendingTransactionId,
                            Timestamp = transaction.SpendingDetails.CreationTime,
                            ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                            Amount = Money.Zero
                        };

                        if (transaction.SpendingDetails.Payments != null)
                        {
                            sentItem.Payments = new List<PaymentDetailModel>();
                            foreach (var payment in transaction.SpendingDetails.Payments)
                            {
                                sentItem.Payments.Add(new PaymentDetailModel
                                {
                                    DestinationAddress = payment.DestinationAddress,
                                    Amount = payment.Amount
                                });

                                sentItem.Amount += payment.Amount;
                            }
                        }

                        // get the change address for this spending transaction
                        var changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                        // find all the spending details containing the spending transaction id and aggregate the sums. 
                        // this is our best shot at finding the total value of inputs for this transaction.
                        var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                        // the fee is calculated as follows: funds in utxo - amount spent - amount sent as change
                        sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                        // mined/staked coins add more coins to the total out 
                        // that makes the fee negative if that's the case ignore the fee
                        if (sentItem.Fee < 0)
                            sentItem.Fee = 0;

                        if (!model.TransactionsHistory.Contains(sentItem, new SentTransactionItemModelComparer()))
                        {
                            model.TransactionsHistory.Add(sentItem);
                        }
                    }
                }

                model.TransactionsHistory = model.TransactionsHistory.OrderByDescending(t => t.Timestamp).ToList();

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the balance of a wallet.
        /// </summary>
        /// <param name="request">The request parameters.</param>        
        /// <returns></returns>
        [Route("balance")]
        [HttpGet]
        public IActionResult GetBalance([FromQuery] WalletBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = this.walletManager.GetAccounts(request.WalletName).ToList();
                foreach (var account in accounts)
                {
                    var result = account.GetSpendableAmount();

                    AccountBalance balance = new AccountBalance
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = result.ConfirmedAmount,
                        AmountUnconfirmed = result.UnConfirmedAmount,
                    };

                    model.AccountsBalances.Add(balance);
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
        /// Gets the maximum spendable balance on an account, along with the fee required to spend it.
        /// </summary>
        /// <param name="request">The request parameters.</param>
        /// <returns></returns>
        [Route("maxbalance")]
        [HttpGet]
        public IActionResult GetMaximumSpendableBalance([FromQuery] WalletMaximumBalanceRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(request.WalletName, request.AccountName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
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
        /// Gets a transaction fee estimate.
        /// Fee can be estimated by creating a <see cref="TransactionBuildContext"/> with no password
        /// and then building the transaction and retreiving the fee from the context.
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
                var context = new TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList())
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
                var context = new TransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList(),
                    request.Password)
                {
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = true
                };

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
        public async Task<IActionResult> SendTransactionAsync([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var transaction = new Transaction(request.Hex);
                var result = await this.broadcasterManager.TryBroadcastAsync(transaction).ConfigureAwait(false);
                if (result == Bitcoin.Broadcasting.Success.Yes)
                {
                    return this.Ok();
                }
                else if (result == Bitcoin.Broadcasting.Success.DontKnow)
                {
                    // wait for propagation
                    var waited = TimeSpan.Zero;
                    var period = TimeSpan.FromSeconds(1);
                    while (TimeSpan.FromSeconds(21) > waited)
                    {
                        // if broadcasts doesn't contain then success
                        var transactionEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());
                        if (transactionEntry != null && transactionEntry.State == Bitcoin.Broadcasting.State.Propagated)
                        {
                            return this.Ok();
                        }
                        await Task.Delay(period).ConfigureAwait(false);
                        waited += period;
                    }
                }

                throw new TimeoutException("Transaction propagation has timed out. Lost connection?");
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Lists all the wallet files found under the default folder.
        /// </summary>
        /// <returns>A list of the wallets files found.</returns>
        [Route("files")]
        [HttpGet]
        public IActionResult ListWalletsFiles()
        {
            try
            {
                (string folderPath, IEnumerable<string> filesNames) result = this.walletManager.GetWalletsFiles();
                WalletFileModel model = new WalletFileModel
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
        /// </summary>
        /// <returns>An account name.</returns>
        [Route("account")]
        [HttpPost]
        public IActionResult CreateNewAccount([FromBody]GetUnusedAccountModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAccount(request.WalletName, request.Password);
                return this.Json(result.Name);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets an unused address.
        /// </summary>
        /// <returns>The last created and unused address or creates a new address (in Base58 format).</returns>
        [Route("address")]
        [HttpGet]
        public IActionResult GetUnusedAddress([FromQuery]GetUnusedAddressModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAddress(new WalletAccountReference(request.WalletName, request.AccountName));
                return this.Json(result.Address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the specified number of unused addresses.
        /// </summary>
        [Route("addresses")]
        [HttpGet]
        public IActionResult GetUnusedAddresses([FromQuery]GetUnusedAddressesModel request)
        {
            Guard.NotNull(request, nameof(request));
            var count = int.Parse(request.Count);

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var result = this.walletManager.GetUnusedAddresses(new WalletAccountReference(request.WalletName, request.AccountName), count);
                return this.Json(result.Select(x => x.Address).ToArray());
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets the extpubkey of the specified account.
        /// </summary>
        [Route("extpubkey")]
        [HttpGet]
        public IActionResult GetExtPubKey([FromQuery]GetExtPubKeyModel request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return BuildErrorResponse(this.ModelState);
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

            var block = this.chain.GetBlock(uint256.Parse(model.Hash));
            this.walletSyncManager.SyncFromHeight(block.Height);
            return this.Ok();
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
