using System;
using System.Collections.Generic;
using System.IO;
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
using Stratis.Bitcoin.Features.SidechainWallet.Interfaces;
using Stratis.Bitcoin.Features.SidechainWallet.Models;
using Stratis.Bitcoin.Features.SidechainWallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.SidechainWallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class SidechainWalletController : Controller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IWalletTransactionHandler walletTransactionHandler;

        public SidechainWalletController(
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
        public IActionResult BuildSidechainTransaction([FromBody] BuildSidechainTransactionRequest request)
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

    }
}