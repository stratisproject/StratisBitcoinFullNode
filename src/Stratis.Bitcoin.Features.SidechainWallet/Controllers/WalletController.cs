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
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
    public class WalletController : Wallet.Controllers.WalletController
    {
        public WalletController(ILoggerFactory loggerFactory, IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IWalletSyncManager walletSyncManager, IConnectionManager connectionManager, Network network, ConcurrentChain chain, IBroadcasterManager broadcasterManager, IDateTimeProvider dateTimeProvider) : base(loggerFactory, walletManager, walletTransactionHandler, walletSyncManager, connectionManager, network, chain, broadcasterManager, dateTimeProvider)
        {
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
                return BuildErrorResponse(this.ModelState);
            }

            try
            {
                var context = (TransactionBuildContext)CreateTransactionBuildContext(request);
                context.SidechainIdentifier = request.SidechainIdentifier;

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