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
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Notifications;

namespace Stratis.Bitcoin.Wallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    public class WalletController : Controller
    {
        private readonly IWalletManager walletManager;

        private readonly IWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        private readonly Network network;

        private readonly ConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

		public WalletController(IWalletManager walletManager, IWalletSyncManager walletSyncManager, ConnectionManager connectionManager, Network network, 
			ConcurrentChain chain)
        {
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = GetWalletFolder(request.FolderPath);
                Mnemonic mnemonic = this.walletManager.CreateWallet(request.Password, walletFolder.FullName, request.Name, request.Network);

                return this.Json(mnemonic.ToString());
            }
            catch (InvalidOperationException e)
            {
                // indicates that this wallet already exists
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Conflict, "This wallet already exists.", e.ToString());
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = GetWalletFolder(request.FolderPath);
                Wallet wallet = this.walletManager.LoadWallet(request.Password, walletFolder.FullName, request.Name);

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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                // get the wallet folder 
                DirectoryInfo walletFolder = GetWalletFolder(request.FolderPath);
                Wallet wallet = this.walletManager.RecoverWallet(request.Password, walletFolder.FullName, request.Name, request.Network, request.Mnemonic, request.CreationDate, null);

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
                    WalletFilePath = wallet.WalletFilePath,
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletHistoryModel model = new WalletHistoryModel { TransactionsHistory = new List<TransactionItemModel>() };

                // get transactions contained in the wallet
                var addresses = this.walletManager.GetHistoryByCoinType(request.WalletName, this.coinType);
                foreach (var address in addresses.Where(a => !a.IsChangeAddress()))
                {
                    foreach (var transaction in address.Transactions)
                    {
                        TransactionItemModel item = new TransactionItemModel();
                        if (transaction.Amount > Money.Zero)
                        {
                            item.Type = TransactionItemType.Received;
                            item.ToAddress = address.Address;
                            item.Amount = transaction.Amount;
                        }
                        else
                        {
                            item.Type = TransactionItemType.Send;
                            item.Amount = Money.Zero;
                            if (transaction.Payments != null)
                            {
                                item.Payments = new List<PaymentDetailModel>();
                                foreach (var payment in transaction.Payments)
                                {
                                    item.Payments.Add(new PaymentDetailModel
                                    {
                                        DestinationAddress = payment.DestinationAddress,
                                        Amount = payment.Amount
                                    });

                                    item.Amount += payment.Amount;
                                }
                            }

                            var changeAddress = addresses.Single(a => a.IsChangeAddress() && a.Transactions.Any(t => t.Id == transaction.Id));
                            item.Fee =  transaction.Amount.Abs() - item.Amount - changeAddress.Transactions.First(t => t.Id == transaction.Id).Amount;
                        }

                        item.Id = transaction.Id;
                        item.Timestamp = transaction.CreationTime;
                        item.ConfirmedInBlock = transaction.BlockHeight;

                        model.TransactionsHistory.Add(item);
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                WalletBalanceModel model = new WalletBalanceModel { AccountsBalances = new List<AccountBalance>() };

                var accounts = this.walletManager.GetAccountsByCoinType(request.WalletName, this.coinType).ToList();
                foreach (var account in accounts)
                {
                    var allTransactions = account.ExternalAddresses.SelectMany(a => a.Transactions)
                        .Concat(account.InternalAddresses.SelectMany(i => i.Transactions)).ToList();

                    AccountBalance balance = new AccountBalance
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = allTransactions.Where(t => t.IsConfirmed()).Sum(t => t.Amount),
                        AmountUnconfirmed = allTransactions.Where(t => !t.IsConfirmed()).Sum(t => t.Amount)
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var transactionResult = this.walletManager.BuildTransaction(request.WalletName, request.AccountName, this.coinType, request.Password, request.DestinationAddress, request.Amount, request.FeeType, request.AllowUnconfirmed);                
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var result = this.walletManager.SendTransaction(request.Hex);
                if (result)
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {
                var result = this.walletManager.GetUnusedAccount(request.WalletName, this.coinType, request.Password);
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
            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                var errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
            }

            try
            {               
                var result = this.walletManager.GetUnusedAddress(request.WalletName, this.coinType, request.AccountName);
                return this.Json(result);
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
        /// <remarks>The folder is created if it doesn't exist.</remarks>
        private static DirectoryInfo GetWalletFolder(string folderPath = null)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = WalletManager.GetDefaultWalletFolderPath();
            }
            return Directory.CreateDirectory(folderPath);
        }


    }
}
