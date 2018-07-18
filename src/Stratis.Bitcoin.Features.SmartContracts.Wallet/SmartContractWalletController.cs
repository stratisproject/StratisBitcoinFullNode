using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    public sealed class SmartContractWalletController : WalletController
    {
        private readonly ILogger logger;

        public SmartContractWalletController(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network, ConcurrentChain chain,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider) :
            base(loggerFactory, walletManager, walletTransactionHandler, walletSyncManager, connectionManager, network, chain, broadcasterManager, dateTimeProvider)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
                return BuildErrorResponse(this.ModelState);

            if (!this.ConnectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                Transaction transaction = this.Network.CreateTransaction(request.Hex);

                var model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (TxOut output in transaction.Outputs)
                {
                    bool isUnspendable = output.ScriptPubKey.IsUnspendable;

                    string address = GetAddressFromScriptPubKey(output);
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = address,
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.BroadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.BroadcasterManager.GetTransaction(transaction.GetHash());
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

        /// <summary>
        /// Retrieves a string that represents the receiving address for an output. For smart contract transactions,
        /// returns the opcode that was sent i.e. OP_CALL or OP_CREATE
        /// </summary>
        private string GetAddressFromScriptPubKey(TxOut output)
        {
            if (output.ScriptPubKey.IsSmartContractExec())
                return output.ScriptPubKey.ToOps().First().Code.ToString();

            if (!output.ScriptPubKey.IsUnspendable)
                return output.ScriptPubKey.GetDestinationAddress(this.Network).ToString();

            return null;
        }
    }
}