using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;

namespace Stratis.Bitcoin.Features.ColdStaking.Controllers
{
    /// <summary>
    /// Controller providing operations for cold staking.
    /// </summary>
    [Route("api/[controller]")]
    public class ColdStakingController : Controller
    {
        public ColdStakingManager ColdStakingManager { get; private set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ColdStakingController(
            ILoggerFactory loggerFactory,
            ColdStakingManager coldStakingManager)
        {
            this.ColdStakingManager = coldStakingManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Gets a cold staking address.
        /// </summary>
        /// <remarks>This method is used to generate cold staking addresses on each machine/wallet
        /// which will then be used with <see cref="SetupColdStaking(SetupColdStakingRequest)"/>.</remarks>
        /// <param name="request">A <see cref="GetColdStakingAddressRequest"/> object containging the parameters
        /// required for generating the cold-staking-address.</param>
        /// <returns>A <see cref="GetColdStakingAddressResponse>"/> object containing the cold staking address.</returns>
        [Route("get-cold-staking-address")]
        [HttpPost]
        public IActionResult GetColdStakingAddress([FromBody]GetColdStakingAddressRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Vhecks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Wallet.Wallet wallet = this.ColdStakingManager.WalletManager.GetWalletByName(request.WalletName);

                var model = new GetColdStakingAddressResponse
                {
                    Address = this.ColdStakingManager.GetColdStakingAddress(wallet, request.WalletPassword, request.IsColdWalletAddress).Address
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
        /// This method spends funds from a normal wallet addresses to the cold staking script. It is expected that this
        /// spend will be detected by both the hot wallet and cold wallet and allow cold staking to occur using this
        /// transaction as input.
        /// </summary>
        /// <param name="request">A <see cref="SetupColdStakingRequest"/> object containing the cold staking setup parameters.</param>
        /// <returns>A <see cref="SetupColdStakingResponse"/> object containing the hex representation of the transaction.</returns>
        /// <seealso cref="ColdStakingManager.GetColdStakingScript(ScriptId, ScriptId)"/>
        [Route("setup-cold-staking")]
        [HttpPost]
        public IActionResult SetupColdStaking([FromBody]SetupColdStakingRequest request)
        {
            Guard.NotNull(request, nameof(request));

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Money amount = string.IsNullOrEmpty(request.Amount) ? null : Money.Parse(request.Amount);
                Money feeAmount = string.IsNullOrEmpty(request.Fees) ? null : Money.Parse(request.Fees);

                TransactionBuildContext context = this.ColdStakingManager.GetSetupBuildContext(request.ColdWalletAddress,
                    request.HotWalletAddress, request.WalletName, request.WalletAccount, request.WalletPassword,
                    amount, feeAmount);

                Transaction transaction = this.ColdStakingManager.WalletTransactionHandler.BuildTransaction(context);

                var model = new SetupColdStakingResponse
                {
                    TransactionHex = transaction.ToHex()
                };

                this.ColdStakingManager.BroadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.ColdStakingManager.BroadcasterManager.GetTransaction(transaction.GetHash());

                if (!string.IsNullOrEmpty(transactionBroadCastEntry?.ErrorMessage))
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
    }
}
