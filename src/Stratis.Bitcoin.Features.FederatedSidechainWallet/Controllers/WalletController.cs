using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.FederatedSidechainWallet.Interfaces;
using Stratis.Bitcoin.Features.FederatedSidechainWallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.FederatedSidechainWallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class FederatedSidechainWalletController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IWalletTransactionHandler walletTransactionHandler;

        public FederatedSidechainWalletController(
            ILoggerFactory loggerFactory,
            IWalletTransactionHandler walletTransactionHandler,
            Network network)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletTransactionHandler = walletTransactionHandler;
            this.network = network;
        }

        /// <summary>
        /// Builds a special transaction for transfering coins from one chain to another
        /// </summary>
        /// <param name="request">The transaction parameters.</param>
        /// <returns>All the details of the transaction, including the hex used to execute it.</returns>
        [Route("build-sidechain-transaction")]
        [HttpPost]
        public IActionResult BuildSidechainTransaction([FromBody] BuildTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // checks the request is valid
            if (!this.ModelState.IsValid)
            {
                return Wallet.Controllers.WalletController.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var standardContext = Wallet.Controllers.WalletController.CreateTransactionBuildContext(request, this.network);
                var context = new TransactionBuildContext(standardContext, request.SidechainIdentifier);

                var transactionResult = this.walletTransactionHandler.BuildCrossChainTransaction(context, this.network);

                var model = new Wallet.Models.WalletBuildTransactionModel
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

    }
}