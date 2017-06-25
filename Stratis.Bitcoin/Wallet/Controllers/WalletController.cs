using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using Stratis.Bitcoin.Common.JsonErrors;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Wallet.Models;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Notifications;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class WalletController : Controller
    {
        private readonly IWalletManager walletManager;

        private readonly IWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        private readonly Network network;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        private readonly DataFolder dataFolder;

        public WalletController(IWalletManager walletManager, IWalletSyncManager walletSyncManager, IConnectionManager connectionManager, Network network,
            ConcurrentChain chain, DataFolder dataFolder)
        {
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.dataFolder = dataFolder;
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = this.GetWalletFolder();
                Mnemonic mnemonic = this.walletManager.CreateWallet(request.Password, request.Name, mnemonic:request.Mnemonic);

                return this.Json(mnemonic.ToString());
            }
            catch (InvalidOperationException e)
            {
                // indicates that this wallet already exists
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, "This wallet already exists.", e.ToString());
            }
            catch (NotSupportedException e)
            {                
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = this.GetWalletFolder();
                Wallet wallet = this.walletManager.LoadWallet(request.Password, request.Name);

                return this.Ok();
            }
            catch (FileNotFoundException e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "This wallet was not found at the specified location.", e.ToString());
            }
            catch (SecurityException e)
            {
                // indicates that the password is wrong
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Wrong password, please try again.", e.ToString());
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = this.GetWalletFolder();
                Wallet wallet = this.walletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate, null);

                // start syncing the wallet from the creation date
                this.walletSyncManager.SyncFrom(request.CreationDate);

                return this.Ok();
            }
            catch (InvalidOperationException e)
            {
                // indicates that this wallet already exists
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, "This wallet already exists.", e.ToString());
            }
            catch (FileNotFoundException e)
            {
                // indicates that this wallet does not exist
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
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
                    IsDecrypted = true                  
                };
                return this.Json(model);

            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel();

                // get transactions contained in the wallet
                var addresses = this.walletManager.GetHistory(request.WalletName);
                foreach (var address in addresses.Where(a => !a.IsChangeAddress()))
                {
                    foreach (var transaction in address.Transactions)
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

                        // add outgoing fund transaction details
                        if (transaction.SpendingDetails != null)
                        {
                            TransactionItemModel sentItem = new TransactionItemModel();
                            sentItem.Type = TransactionItemType.Send;
                            sentItem.Id = transaction.SpendingDetails.TransactionId;
                            sentItem.Timestamp = transaction.SpendingDetails.CreationTime;
                            sentItem.ConfirmedInBlock = transaction.SpendingDetails.BlockHeight;

                            sentItem.Amount = Money.Zero;
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
                            var changeAddress = addresses.SingleOrDefault(a => a.IsChangeAddress() && a.Transactions.Any(t => t.Id == transaction.SpendingDetails.TransactionId));

                            // the fee is calculated as follows: fund in utxo - amount spent - amount sent as change
                            sentItem.Fee = transaction.Amount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transactions.First(t => t.Id == transaction.SpendingDetails.TransactionId).Amount);

                            // mined/staked coins add more coins to the total out 
                            // that makes the fee negative if thats the case ignore the fee
                            if (sentItem.Fee < 0)
                                sentItem.Fee = 0;
                            
                            model.TransactionsHistory.Add(sentItem);
                        }
                    }
                }

                model.TransactionsHistory = model.TransactionsHistory.OrderByDescending(t => t.Timestamp).ToList();
                return this.Json(model);
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel();

                var accounts = this.walletManager.GetAccounts(request.WalletName).ToList();
                foreach (var account in accounts)
                {
                    var allTransactions = account.ExternalAddresses.SelectMany(a => a.Transactions)
                        .Concat(account.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

                    AccountBalance balance = new AccountBalance
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = allTransactions.Sum(t => t.SpendableAmount(true)),
                        AmountUnconfirmed = allTransactions.Sum(t => t.SpendableAmount(false)) - allTransactions.Sum(t => t.SpendableAmount(true)),
                    };

                    model.AccountsBalances.Add(balance);
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var transactionResult = this.walletManager.BuildTransaction(request.WalletName, request.AccountName, request.Password, request.DestinationAddress, request.Amount, FeeParser.Parse(request.FeeType), request.AllowUnconfirmed ? 0 : 1);
                var model = new WalletBuildTransactionModel
                {
                    Hex = transactionResult.hex,
                    Fee = transactionResult.fee,
                    TransactionId = transactionResult.transactionId
                };
                return this.Json(model);
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {                
                if (this.walletManager.SendTransaction(request.Hex))
                {
                    return this.Ok();
                }

                return this.StatusCode((int)HttpStatusCode.BadRequest);
            }
            catch (Exception e)
            {
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
                DirectoryInfo walletsFolder = GetWalletFolder();

                WalletFileModel model = new WalletFileModel
                {
                    WalletsPath = walletsFolder.FullName,
                    WalletsFiles = Directory.EnumerateFiles(walletsFolder.FullName, "*.json", SearchOption.TopDirectoryOnly).Select(p => Path.GetFileName(p))
                };

                return this.Json(model);
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var result = this.walletManager.GetUnusedAccount(request.WalletName, request.Password);
                return this.Json(result.Name);
            }
            catch (Exception e)
            {
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
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var result = this.walletManager.GetUnusedAddress(request.WalletName, request.AccountName);
                return this.Json(result.Address);
            }
            catch (Exception e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a folder.
        /// </summary>
        /// <returns>The path folder of the folder.</returns>
        /// <remarks>The folder will always be the same as the running node.</remarks>
        private DirectoryInfo GetWalletFolder()
        {
            return Directory.CreateDirectory(this.dataFolder.WalletPath);
        }
    }
}
