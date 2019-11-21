namespace Stratis.Bitcoin.Features.Wallet.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using NBitcoin;
    using Stratis.Bitcoin.Builder.Feature;
    using Stratis.Bitcoin.Features.Wallet.Interfaces;
    using Stratis.Bitcoin.Features.Wallet.Models;
    using Stratis.Bitcoin.Features.Wallet.Services;
    using Stratis.Bitcoin.Utilities.JsonErrors;

    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public class WalletController : FeatureControllerBase
    {
        private readonly IWalletService walletService;
        private readonly IWalletManager walletManager;
        private readonly IWalletSyncManager walletSyncManager;
        private readonly ChainIndexer chainIndexer;

        public WalletController(
            ILoggerFactory loggerFactory,
            IWalletService walletService,
            IWalletManager walletManager,
            IWalletSyncManager walletSyncManager,
            ChainIndexer chainIndexer)
            : base(loggerFactory.CreateLogger(typeof(WalletController).FullName))
        {
            this.walletService = walletService;
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;
            this.chainIndexer = chainIndexer;
        }

        /// <summary>
        /// Generates a mnemonic to use for an HD wallet.
        /// </summary>
        /// <param name="language">The language for the words in the mnemonic. The options are: English, French, Spanish, Japanese, ChineseSimplified and ChineseTraditional. Defaults to English.</param>
        /// <param name="wordCount">The number of words in the mnemonic. The options are: 12,15,18,21 or 24. Defaults to 12.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the generated mnemonic.</returns>
        [Route("mnemonic")]
        [HttpGet]
        public async Task<IActionResult> GenerateMnemonic([FromQuery] string language = "English", int wordCount = 12,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(new {Language = language, WordCount = wordCount},
                cancellationToken, (req, token) =>
                    // Generate the Mnemonic
                    this.Json(new Mnemonic(language, (WordCount) wordCount).ToString()));
        }

        /// <summary>
        /// Creates a new wallet on this full node.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to create a wallet.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WalletCreationRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.CreateWallet(req, token)));
        }

        /// <summary>
        /// Signs a message and returns the signature.
        /// </summary>
        /// <param name="request">The object containing the parameters used to sign a message.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A JSON object containing the generated signature.</returns>
        [Route("signmessage")]
        [HttpPost]
        public async Task<IActionResult> SignMessage([FromBody] SignMessageRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                string signature =
                    this.walletManager.SignMessage(req.Password, req.WalletName, req.ExternalAddress, req.Message);
                return this.Json(signature);
            });
        }

        /// <summary>
        /// Verifies the signature of a message.
        /// </summary>
        /// <param name="request">The object containing the parameters verify a signature.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the result of the verification.</returns>
        [Route("verifymessage")]
        [HttpPost]
        public async Task<IActionResult> VerifyMessage([FromBody] VerifyRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                bool result =
                    this.walletManager.VerifySignedMessage(request.ExternalAddress, req.Message, req.Signature);
                return this.Json(result.ToString());
            });
        }

        /// <summary>
        /// Loads a previously created wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to load an existing wallet</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Ok or Error result</returns>
        [Route("load")]
        [HttpPost]
        public async Task<IActionResult> Load([FromBody] WalletLoadRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken, async (req, token) =>
            {
                await this.walletService.LoadWallet(req, token);
                return Ok();
            });
        }

        /// <summary>
        /// Recovers an existing wallet.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover")]
        [HttpPost]
        public async Task<IActionResult> Recover([FromBody] WalletRecoveryRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken, async (req, token) =>
            {
                await this.walletService.RecoverWallet(req, token);
                return Ok();
            });
        }

        /// <summary>
        /// Recovers a wallet using its extended public key. Note that the recovered wallet will not have a private key and is
        /// only suitable for returning the wallet history using further API calls.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet using its extended public key.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover-via-extpubkey")]
        [HttpPost]
        public async Task<IActionResult> RecoverViaExtPubKey([FromBody] WalletExtPubRecoveryRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken, async (req, token) =>
            {
                await this.walletService.RecoverViaExtPubKey(req, token);
                return Ok();
            });
        }

        /// <summary>
        /// Gets some general information about a wallet. This includes the network the wallet is for,
        /// the creation date and time for the wallet, the height of the blocks the wallet currently holds,
        /// and the number of connected nodes.
        /// </summary>
        /// <param name="request">The name of the wallet to get the information for.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A JSON object containing the wallet information.</returns>
        [Route("general-info")]
        [HttpGet]
        public Task<IActionResult> GetGeneralInfo([FromQuery] WalletName request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.Execute(request, cancellationToken, async (req, token) =>
                this.Json(await this.walletService.GetWalletGeneralInfo(req.Name, token)));
        }

        /// <summary>
        /// Get the transaction count for the specified Wallet and Account.
        /// </summary>
        /// <param name="request">The Transaction Count request Object</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>Transaction Count</returns>
        [Route("transactionCount")]
        [HttpGet]
        public async Task<IActionResult> GetTransactionCount([FromQuery] WalletTransactionCountRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken,
                (req, token) => this.Json(new
                {
                    TransactionCount = this.walletManager.GetTransactionCount(req.WalletName, req.AccountName)
                }));
        }

        /// <summary>
        /// Gets the history of a wallet. This includes the transactions held by the entire wallet
        /// or a single account if one is specified.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's history.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the wallet history.</returns>
        [Route("history")]
        [HttpGet]
        public async Task<IActionResult> GetHistory([FromQuery] WalletHistoryRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.GetHistory(req, token)));
        }


        /// <summary>
        /// Gets the balance of a wallet in STRAT (or sidechain coin). Both the confirmed and unconfirmed balance are returned.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's balance.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A JSON object containing the wallet balance.</returns>
        [Route("balance")]
        [HttpGet]
        public async Task<IActionResult> GetBalance([FromQuery] WalletBalanceRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.GetBalance(req.WalletName, req.AccountName,
                    req.IncludeBalanceByAddress, token))
            );
        }

        /// <summary>
        /// Gets the balance at a specific wallet address in STRAT (or sidechain coin).
        /// Both the confirmed and unconfirmed balance are returned.
        /// This method gets the UTXOs at the address which the wallet can spend.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the balance
        /// at a specific wallet address.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A JSON object containing the balance, fee, and an address for the balance.</returns>
        [Route("received-by-address")]
        [HttpGet]
        public async Task<IActionResult> GetReceivedByAddress([FromQuery] ReceivedByAddressRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) =>
                    this.Json(await this.walletService.GetReceivedByAddress(request.Address, cancellationToken)));
        }

        /// <summary>
        /// Gets the maximum spendable balance for an account along with the fee required to spend it.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the
        /// maximum spendable balance on an account.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the maximum spendable balance for an account
        /// along with the fee required to spend it.</returns>
        [Route("maxbalance")]
        [HttpGet]
        public async Task<IActionResult> GetMaximumSpendableBalance([FromQuery] WalletMaximumBalanceRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) =>
                    this.Json(await this.walletService.GetMaximumSpendableBalance(request, cancellationToken)));
        }

        /// <summary>
        /// Gets the spendable transactions for an account with the option to specify how many confirmations
        /// a transaction needs to be included.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the spendable
        /// transactions for an account.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the spendable transactions for an account.</returns>
        [Route("spendable-transactions")]
        [HttpGet]
        public async Task<IActionResult> GetSpendableTransactions([FromQuery] SpendableTransactionsRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => Json(await this.walletService.GetSpendableTransactions(req, token)));
        }

        /// <summary>
        /// Gets a fee estimate for a specific transaction.
        /// Fee can be estimated by creating a <see cref="TransactionBuildContext"/> with no password
        /// and then building the transaction and retrieving the fee from the context.
        /// </summary>
        /// <param name="request">An object containing the parameters used to estimate the fee
        /// for a specific transaction.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-txfee")]
        [HttpPost]
        public async Task<IActionResult> GetTransactionFeeEstimate([FromBody] TxFeeEstimateRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => Json(await this.walletService.GetTransactionFeeEstimate(req, token)));
        }

        /// <summary>
        /// Builds a transaction and returns the hex to use when executing the transaction.
        /// </summary>
        /// <param name="request">An object containing the parameters used to build a transaction.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object including the transaction ID, the hex used to execute
        /// the transaction, and the transaction fee.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public async Task<IActionResult> BuildTransaction([FromBody] BuildTransactionRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => Json(await this.walletService.BuildTransaction(req, token)));
        }

        /// <summary>
        /// Sends a transaction that has already been built.
        /// Use the /api/Wallet/build-transaction call to create transactions.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters used to a send transaction request.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing information about the sent transaction.</returns>
        [Route("send-transaction")]
        [HttpPost]
        public async Task<IActionResult> SendTransaction([FromBody] SendTransactionRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => Json(await this.walletService.SendTransaction(req, token)));
        }

        /// <summary>
        /// Lists all the files found in the database
        /// </summary>
        /// <returns>A JSON object containing the available wallet name
        /// </returns>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [Route("list-wallets")]
        [HttpGet]
        public async Task<IActionResult> ListWallets(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync((object) null, cancellationToken, (req, token) =>
                this.Json(new WalletInfoModel(this.walletManager.GetWalletsNames())), false);
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
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the name of the new account or an existing account
        /// containing no transactions.</returns>
        [Route("account")]
        [HttpPost]
        public async Task<IActionResult> CreateNewAccount([FromBody] GetUnusedAccountModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                try
                {
                    HdAccount result = this.walletManager.GetUnusedAccount(request.WalletName, request.Password);
                    return this.Json(result.Name);
                }
                catch (CannotAddAccountToXpubKeyWalletException e)
                {
                    this.Logger.LogError("Exception occurred: {0}", e.ToString());
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, e.Message, string.Empty);
                }
            });
        }

        /// <summary>
        /// Gets a list of accounts for the specified wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to list the accounts for a wallet.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing a list of accounts for the specified wallet.</returns>
        [Route("accounts")]
        [HttpGet]
        public async Task<IActionResult> ListAccounts([FromQuery] ListAccountsModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                IEnumerable<HdAccount> result = this.walletManager.GetAccounts(request.WalletName);
                return this.Json(result.Select(a => a.Name));
            });
        }

        /// <summary>
        /// Gets an unused address (in the Base58 format) for a wallet account. This address will not have been assigned
        /// to any known UTXO (neither to pay funds into the wallet or to pay change back to the wallet).
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to retrieve an
        /// unused address for a wallet account.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the last created and unused address (in Base58 format).</returns>
        [Route("unusedaddress")]
        [HttpGet]
        public async Task<IActionResult> GetUnusedAddress([FromQuery] GetUnusedAddressModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                HdAddress result = this.walletManager.GetUnusedAddress(new WalletAccountReference(
                    request.WalletName,
                    request.AccountName));
                return this.Json(result.Address);
            });
        }

        /// <summary>
        /// Gets a specified number of unused addresses (in the Base58 format) for a wallet account. These addresses
        /// will not have been assigned to any known UTXO (neither to pay funds into the wallet or to pay change back
        /// to the wallet).
        /// </summary>
        /// <returns>A JSON object containing the required amount of unused addresses (in Base58 format).</returns>
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// unused addresses for a wallet account.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [Route("unusedaddresses")]
        [HttpGet]
        public async Task<IActionResult> GetUnusedAddresses([FromQuery] GetUnusedAddressesModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                var result = this.walletManager.GetUnusedAddresses(
                        new WalletAccountReference(request.WalletName, req.AccountName), int.Parse(req.Count))
                    .Select(x => x.Address).ToArray();

                return this.Json(result);
            });
        }

        /// <summary>
        /// Gets all addresses for a wallet account.
        /// </summary>
        /// <returns>A JSON object containing all addresses for a wallet account (in Base58 format).</returns>
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// all addresses for a wallet account.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [Route("addresses")]
        [HttpGet]
        public async Task<IActionResult> GetAllAddresses([FromQuery] GetAllAddressesModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.GetAllAddresses(req, token)));
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
        /// </summary>
        /// <returns>A JSON object containing all removed transactions identified by their
        /// transaction ID and creation time.</returns>
        /// <param name="request">An object containing the necessary parameters to remove transactions
        /// from a wallet. The includes several options for specifying the transactions to remove.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [Route("remove-transactions")]
        [HttpDelete]
        public async Task<IActionResult> RemoveTransactions([FromQuery] RemoveTransactionsModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.RemoveTransactions(req, token)));
        }

        /// <summary>
        /// Gets the extended public key of a specified wallet account.
        /// the extended public key for a wallet account
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to retrieve.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A JSON object containing the extended public key for a wallet account.</returns>
        [Route("extpubkey")]
        [HttpGet]
        public async Task<IActionResult> GetExtPubKey([FromQuery] GetExtPubKeyModel request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken,
                (req, token) =>
                    this.Json(this.walletManager.GetExtPubKey(new WalletAccountReference(request.WalletName,
                        request.AccountName))));
        }

        /// <summary>
        /// Requests the node resyncs from a block specified by its block hash.
        /// Internally, the specified block is taken as the new wallet tip
        /// and all blocks after it are resynced.
        /// </summary>
        /// <param name="model">The Hash of the block to Sync From</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A value of Ok if the re-sync was successful.</returns>
        [HttpPost]
        [Route("sync")]
        public async Task<IActionResult> Sync([FromBody] HashModel model,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(model, cancellationToken, (req, token) =>
            {
                ChainedHeader block = this.chainIndexer.GetHeader(uint256.Parse(model.Hash));
                if (block == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                        $"Block with hash {model.Hash} was not found on the blockchain.", string.Empty);
                }

                this.walletSyncManager.SyncFromHeight(block.Height);
                return this.Ok();
            });
        }

        /// <summary>
        /// Request the node resyncs starting from a given date and time.
        /// Internally, the first block created on or after the supplied date and time
        /// is taken as the new wallet tip and all blocks after it are resynced.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters
        /// to request a resync.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        /// <returns>A value of Ok if the resync was successful.</returns>
        [HttpPost]
        [Route("sync-from-date")]
        public async Task<IActionResult> SyncFromDate([FromBody] WalletSyncRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ExecuteAsAsync(request, cancellationToken, (req, token) =>
            {
                if (!request.All)
                {
                    this.walletSyncManager.SyncFromDate(request.Date, request.WalletName);
                }
                else
                {
                    this.walletSyncManager.SyncFromHeight(0, request.WalletName);
                }

                return this.Ok();
            });
        }

        [Route("wallet-stats")]
        [HttpGet]
        public async Task<IActionResult> WalletStats([FromQuery] WalletStatsRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.GetWalletStats(req, token)));
        }

        /// <summary>Creates requested amount of UTXOs each of equal value.</summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        /// <param name="request">An object containing the necessary parameters.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [HttpPost]
        [Route("splitcoins")]
        public async Task<IActionResult> SplitCoins([FromBody] SplitCoinsRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.SplitCoins(req, token)));
        }


        /// <summary>Splits and distributes UTXOs across wallet addresses</summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        /// <param name="request">An object containing the necessary parameters.</param>
        /// <param name="cancellationToken">The Cancellation Token</param>
        [HttpPost]
        [Route("distribute-utxos")]
        public async Task<IActionResult> DistributeUtxos([FromBody] DistributeUtxosRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.Execute(request, cancellationToken,
                async (req, token) => this.Json(await this.walletService.DistributeUtxos(req, token)));
        }

        private TransactionItemModel FindSimilarReceivedTransactionOutput(List<TransactionItemModel> items,
            TransactionData transaction)
        {
            TransactionItemModel existingTransaction = items.FirstOrDefault(i => i.Id == transaction.Id &&
                                                                                 i.Type == TransactionItemType
                                                                                     .Received &&
                                                                                 i.ConfirmedInBlock ==
                                                                                 transaction.BlockHeight);
            return existingTransaction;
        }
    }
}