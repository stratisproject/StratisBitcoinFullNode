using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.ColdStaking.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
        private readonly IWalletTransactionHandler walletTransactionHandler;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ColdStakingController(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(walletTransactionHandler, nameof(walletTransactionHandler));

            this.ColdStakingManager = walletManager as ColdStakingManager;
            Guard.NotNull(this.ColdStakingManager, nameof(this.ColdStakingManager));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletTransactionHandler = walletTransactionHandler;
        }

        /// <summary>
        /// Gets general information related to cold staking.
        /// </summary>
        /// <param name="request">A <see cref="GetColdStakingInfoRequest"/> object containing the
        /// parameters  required to obtain cold staking information.</param>
        /// <returns>A <see cref="GetColdStakingInfoResponse"/> object containing the cold staking information.</returns>
        [Route("cold-staking-info")]
        [HttpGet]
        public IActionResult GetColdStakingInfo([FromQuery]GetColdStakingInfoRequest request)
        {
            Guard.NotNull(request, nameof(request));

            this.logger.LogTrace("({0}:'{1}')", nameof(request), request);

            // Checks that the request is valid.
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                GetColdStakingInfoResponse model = this.ColdStakingManager.GetColdStakingInfo(request.WalletName);

                this.logger.LogTrace("(-):'{0}'", model);
                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[ERROR]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Creates a cold staking account.
        /// </summary>
        /// <remarks>This method is used to create cold staking accounts on each machine/wallet, if required,
        /// prior to calling <see cref="GetColdStakingAddress"/>.</remarks>
        /// <param name="request">A <see cref="CreateColdStakingAccountRequest"/> object containing the parameters
        /// required for creating the cold staking account.</param>
        /// <returns>A <see cref="CreateColdStakingAccountResponse>"/> object containing the account name.</returns>
        [Route("cold-staking-account")]
        [HttpPost]
        public IActionResult CreateColdStakingAccount([FromBody]CreateColdStakingAccountRequest request)
        {
            Guard.NotNull(request, nameof(request));

            this.logger.LogTrace("({0}:'{1}')", nameof(request), request);

            // Checks that the request is valid.
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var model = new CreateColdStakingAccountResponse
                {
                    AccountName = this.ColdStakingManager.GetOrCreateColdStakingAccount(request.WalletName, request.IsColdWalletAccount, request.WalletPassword).Name
                };

                this.logger.LogTrace("(-):'{0}'", model);
                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[ERROR]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Gets a cold staking address. Assumes that the cold staking account exists.
        /// </summary>
        /// <remarks>This method is used to generate cold staking addresses on each machine/wallet
        /// which will then be used with <see cref="SetupColdStaking(SetupColdStakingRequest)"/>.</remarks>
        /// <param name="request">A <see cref="GetColdStakingAddressRequest"/> object containing the parameters
        /// required for generating the cold staking address.</param>
        /// <returns>A <see cref="GetColdStakingAddressResponse>"/> object containing the cold staking address.</returns>
        [Route("cold-staking-address")]
        [HttpGet]
        public IActionResult GetColdStakingAddress([FromQuery]GetColdStakingAddressRequest request)
        {
            Guard.NotNull(request, nameof(request));

            this.logger.LogTrace("({0}:'{1}')", nameof(request), request);

            // Checks that the request is valid.
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                var model = new GetColdStakingAddressResponse
                {
                    Address = this.ColdStakingManager.GetFirstUnusedColdStakingAddress(request.WalletName, request.IsColdWalletAddress)?.Address
                };

                if (model.Address == null)
                    throw new WalletException("The cold staking account does not exist.");

                this.logger.LogTrace("(-):'{0}'", model);
                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[ERROR]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Spends funds from a normal wallet addresses to the cold staking script. It is expected that this
        /// spend will be detected by both the hot wallet and cold wallet and allow cold staking to occur using this
        /// transaction's output as input.
        /// </summary>
        /// <param name="request">A <see cref="SetupColdStakingRequest"/> object containing the cold staking setup parameters.</param>
        /// <returns>A <see cref="SetupColdStakingResponse"/> object containing the hex representation of the transaction.</returns>
        /// <seealso cref="ColdStakingManager.GetColdStakingScript(ScriptId, ScriptId)"/>
        [Route("setup-cold-staking")]
        [HttpPost]
        public IActionResult SetupColdStaking([FromBody]SetupColdStakingRequest request)
        {
            Guard.NotNull(request, nameof(request));

            this.logger.LogTrace("({0}:'{1}')", nameof(request), request);

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Money amount = Money.Parse(request.Amount);
                Money feeAmount = Money.Parse(request.Fees);

                Transaction transaction = this.ColdStakingManager.GetColdStakingSetupTransaction(
                    this.walletTransactionHandler, request.ColdWalletAddress, request.HotWalletAddress,
                    request.WalletName, request.WalletAccount, request.WalletPassword, amount, feeAmount);

                var model = new SetupColdStakingResponse
                {
                    TransactionHex = transaction.ToHex()
                };

                this.logger.LogTrace("(-):'{0}'", model);
                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[ERROR]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Spends funds from the cold staking wallet account back to a normal wallet addresses. It is expected that this
        /// spend will be detected by both the hot wallet and cold wallet and reduce the amount available for cold staking.
        /// </summary>
        /// <param name="request">A <see cref="ColdStakingWithdrawalRequest"/> object containing the cold staking withdrawal parameters.</param>
        /// <returns>A <see cref="ColdStakingWithdrawalResponse"/> object containing the hex representation of the transaction.</returns>
        /// <seealso cref="ColdStakingManager.GetColdStakingScript(ScriptId, ScriptId)"/>
        [Route("cold-staking-withdrawal")]
        [HttpPost]
        public IActionResult ColdStakingWithdrawal([FromBody]ColdStakingWithdrawalRequest request)
        {
            Guard.NotNull(request, nameof(request));

            this.logger.LogTrace("({0}:'{1}')", nameof(request), request);

            // Checks the request is valid.
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODEL_STATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            try
            {
                Money amount = Money.Parse(request.Amount);
                Money feeAmount = Money.Parse(request.Fees);

                Transaction transaction = this.ColdStakingManager.GetColdStakingWithdrawalTransaction(this.walletTransactionHandler,
                    request.ReceivingAddress, request.WalletName, request.WalletPassword, amount, feeAmount);

                var model = new ColdStakingWithdrawalResponse
                {
                    TransactionHex = transaction.ToHex()
                };

                this.logger.LogTrace("(-):'{0}'", model);

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                this.logger.LogTrace("(-)[ERROR]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }
    }
}
