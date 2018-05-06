using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.FederatedPeg.Features.SidechainRuntime.Models;

//This is experimental while we are waiting for a generic OP_RETURN function in the full node wallet.

namespace Stratis.FederatedPeg.Features.SidechainRuntime
{
    [Route("api/[controller]")]
    public class SidechainRuntimeController : Controller
    {
        private ISidechainRuntimeManager sidechainRuntimeManager;
        private IWalletTransactionHandler walletTransactionHandler;
        private IWalletManager walletManager;
        private IBroadcasterManager broadcasterManager;
        private IConnectionManager connectionManager;

        private Network network;

        public SidechainRuntimeController(ISidechainRuntimeManager sidechainRuntimeManager, Network network, IWalletTransactionHandler walletTransactionHandler,
            IWalletManager walletManager, IBroadcasterManager broadcasterManager, IConnectionManager connectionManager)
        {
            this.sidechainRuntimeManager = sidechainRuntimeManager;
            this.network = network;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletManager = walletManager;
            this.broadcasterManager = broadcasterManager;
            this.connectionManager = connectionManager;
        }

        [Route("build-transaction")]
        [HttpPost]
        public IActionResult BuildTransaction([FromBody] WithdrawFundsFromSidechainRequest request)
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

                byte[] bytes = Encoding.UTF8.GetBytes(request.MainchainDestinationAddress);
                var dataScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);

                var context = new FedPegTransactionBuildContext(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    new[] { new Recipient { Amount = request.Amount, ScriptPubKey = destination } }.ToList(),
                    request.Password, dataScript)
                {
                    TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = request.ShuffleOutputs ?? true // We shuffle transaction outputs by default as it's better for anonymity.
                };

                if (!string.IsNullOrEmpty(request.FeeType))
                {
                    context.FeeType = FeeParser.Parse(request.FeeType);
                }

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
                return BuildErrorResponse(this.ModelState);
            }

            if (!this.connectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                var transaction = new Transaction(request.Hex);

                //WalletSendTransactionModel model = new WalletSendTransactionModel
                //{
                //    TransactionId = transaction.GetHash(),
                //    Outputs = new List<TransactionOutputModel>()
                //};

                //foreach (var output in transaction.Outputs)
                //{
                //    model.Outputs.Add(new TransactionOutputModel
                //    {
                //        Address = output.ScriptPubKey.GetDestinationAddress(this.network).ToString(),
                //        Amount = output.Value,
                //    });
                //}

                //this.walletManager.ProcessTransaction(transaction, null, null, false);

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                //return this.Json(model);
                return this.Ok();
            }
            catch (Exception e)
            {
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
