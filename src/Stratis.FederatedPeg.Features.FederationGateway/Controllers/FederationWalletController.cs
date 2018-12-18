using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Features.FederationGateway.Controllers
{
    public static class FederationWalletRouteEndPoint
    {
        public const string GeneralInfo = "general-info";
        public const string Balance = "balance";
        public const string Sync = "sync";
        public const string ImportKey = "import-key";
        public const string EnableFederation = "enable-federation";
        public const string RemoveTransactions = "remove-transactions";
    }

    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class FederationWalletController : Controller
    {
        private readonly IFederationWalletManager walletManager;

        private readonly IFederationWalletSyncManager walletSyncManager;

        private readonly CoinType coinType;

        private readonly IConnectionManager connectionManager;

        private readonly ConcurrentChain chain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public FederationWalletController(
            ILoggerFactory loggerFactory,
            IFederationWalletManager walletManager,
            IFederationWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chain = chain;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [Route(FederationWalletRouteEndPoint.GeneralInfo)]
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

        [Route(FederationWalletRouteEndPoint.Balance)]
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

                (Money ConfirmedAmount, Money UnConfirmedAmount) result = wallet.GetSpendableAmount();

                var balance = new AccountBalanceModel
                {
                    CoinType = this.coinType,
                    AmountConfirmed = result.ConfirmedAmount,
                    AmountUnconfirmed = result.UnConfirmedAmount,
                };

                var model = new WalletBalanceModel();
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
        /// Starts sending block to the wallet for synchronisation.
        /// This is for demo and testing use only.
        /// </summary>
        /// <param name="model">The hash of the block from which to start syncing.</param>
        [HttpPost]
        [Route(FederationWalletRouteEndPoint.Sync)]
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
        [Route(FederationWalletRouteEndPoint.ImportKey)]
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
                this.walletManager.ImportMemberKey(request.Password, request.Mnemonic, request.Passphrase);
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Provide the federation wallet's credentials so that it can sign transactions.
        /// </summary>
        /// <param name="request">The password of the federation wallet.</param>
        /// <returns>An <see cref="OkResult"/> object that produces a status code 200 HTTP response.</returns>
        [Route(FederationWalletRouteEndPoint.EnableFederation)]
        [HttpPost]
        public IActionResult EnableFederation([FromBody]EnableFederationRequest request)
        {
            Guard.NotNull(request, nameof(request));

            try
            {
                if (!this.ModelState.IsValid)
                {
                    IEnumerable<string> errors = this.ModelState.Values.SelectMany(e => e.Errors.Select(m => m.ErrorMessage));
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Formatting error", string.Join(Environment.NewLine, errors));
                }

                FederationWallet wallet = this.walletManager.GetWallet();

                // Check the password
                try
                {
                    Key.Parse(wallet.EncryptedSeed, request.Password, wallet.Network);
                }
                catch (Exception ex)
                {
                    throw new SecurityException(ex.Message);
                }

                this.walletManager.Secret = new WalletSecret() { WalletPassword = request.Password };
                return this.Ok();
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Remove all transactions from the wallet.
        /// </summary>
        [Route(FederationWalletRouteEndPoint.RemoveTransactions)]
        [HttpDelete]
        public IActionResult RemoveTransactions([FromQuery]RemoveFederationTransactionsModel request)
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

                result = this.walletManager.RemoveAllTransactions();

                // If the user chose to resync the wallet after removing transactions.
                if (result.Any() && request.ReSync)
                {
                    // From the list of removed transactions, check which one is the oldest and retrieve the block right before that time.
                    DateTimeOffset earliestDate = result.Min(r => r.creationTime);
                    ChainedHeader chainedHeader = this.chain.GetBlock(this.chain.GetHeightAtTime(earliestDate.DateTime));

                    // Update the wallet and save it to the file system.
                    FederationWallet wallet = this.walletManager.GetWallet();
                    wallet.LastBlockSyncedHeight = chainedHeader.Height;
                    wallet.LastBlockSyncedHash = chainedHeader.HashBlock;
                    this.walletManager.SaveWallet();

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