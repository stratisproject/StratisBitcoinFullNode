using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    [Route("api/[controller]")]
    public sealed class SmartContractWalletController : Controller
    {
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IConnectionManager connectionManager;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IReceiptRepository receiptRepository;
        private readonly IWalletManager walletManager;
        private readonly ISmartContractTransactionService smartContractTransactionService;

        public SmartContractWalletController(
            IBroadcasterManager broadcasterManager,
            ICallDataSerializer callDataSerializer,
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            Network network,
            IReceiptRepository receiptRepository,
            IWalletManager walletManager,
            ISmartContractTransactionService smartContractTransactionService)
        {
            this.broadcasterManager = broadcasterManager;
            this.callDataSerializer = callDataSerializer;
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.receiptRepository = receiptRepository;
            this.walletManager = walletManager;
            this.smartContractTransactionService = smartContractTransactionService;
        }

        private IEnumerable<HdAddress> GetAccountAddressesWithBalance(string walletName)
        {
            return this.walletManager
                .GetSpendableTransactionsInWallet(walletName)
                .GroupBy(x => x.Address)
                .Where(grouping => grouping.Sum(x => x.Transaction.SpendableAmount(true)) > 0)
                .Select(grouping => grouping.Key);
        }

        [Route("account-addresses")]
        [HttpGet]
        public IActionResult GetAccountAddresses(string walletName)
        {
            if (string.IsNullOrWhiteSpace(walletName))
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "No wallet name", "No wallet name provided");

            try
            {
                IEnumerable<string> addresses = this.GetAccountAddressesWithBalance(walletName)
                    .Select(a => a.Address);

                if (!addresses.Any())
                {
                    HdAccount account = this.walletManager.GetAccounts(walletName).First();

                    var walletAccountReference = new WalletAccountReference(walletName, account.Name);

                    HdAddress nextAddress = this.walletManager.GetUnusedAddress(walletAccountReference);

                    return this.Json(new[] { nextAddress.Address });
                }

                return this.Json(addresses);
            }
            catch (WalletException e)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("address-balance")]
        [HttpGet]
        public IActionResult GetAddressBalance(string address)
        {
            AddressBalance balance = this.walletManager.GetAddressBalance(address);

            return this.Json(balance.AmountConfirmed.ToUnit(MoneyUnit.Satoshi));
        }

        [Route("history")]
        [HttpGet]
        public IActionResult GetHistory(string walletName, string address)
        {
            if (string.IsNullOrWhiteSpace(walletName))
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "No wallet name", "No wallet name provided");

            if (string.IsNullOrWhiteSpace(address))
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "No address", "No address provided");

            try
            {
                var transactionItems = new List<ContractTransactionItem>();

                HdAccount account = this.walletManager.GetAccounts(walletName).First();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(walletName, account.Name);

                // Wallet manager returns only 1 when an account name is specified.
                AccountHistory accountHistory = accountsHistory.First();

                List<FlatHistory> items = accountHistory.History.OrderByDescending(o => o.Transaction.CreationTime).Where(x=>x.Address.Address == address).ToList();

                // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();

                foreach (FlatHistory item in history)
                {
                    TransactionData transaction = item.Transaction;

                    // Record a receive transaction
                    transactionItems.Add(new ContractTransactionItem
                    {
                        Amount = transaction.Amount.ToUnit(MoneyUnit.Satoshi),
                        BlockHeight = transaction.BlockHeight,
                        Hash = transaction.Id,
                        Type = ReceivedTransactionType(transaction),
                        To = address
                    });

                    // Add outgoing transaction details
                    if (transaction.SpendingDetails != null)
                    {
                        // Get if it's an SC transaction
                        PaymentDetails scPayment = transaction.SpendingDetails.Payments?.FirstOrDefault(x => x.DestinationScriptPubKey.IsSmartContractExec());

                        if (scPayment != null)
                        {
                            if (scPayment.DestinationScriptPubKey.IsSmartContractCreate())
                            {
                                // Create a record for a Create transaction
                                Receipt receipt = this.receiptRepository.Retrieve(transaction.SpendingDetails.TransactionId);
                                transactionItems.Add(new ContractTransactionItem
                                {
                                    Amount = scPayment.Amount.ToUnit(MoneyUnit.Satoshi),
                                    BlockHeight = transaction.SpendingDetails.BlockHeight,
                                    Type = ContractTransactionItemType.ContractCreate,
                                    Hash = transaction.SpendingDetails.TransactionId,
                                    To = receipt?.NewContractAddress?.ToBase58Address(this.network) ?? ""
                                });
                            }
                            else
                            {
                                // Create a record for a Call transaction
                                Result<ContractTxData> txData = this.callDataSerializer.Deserialize(scPayment.DestinationScriptPubKey.ToBytes());

                                transactionItems.Add(new ContractTransactionItem
                                {
                                    Amount = scPayment.Amount.ToUnit(MoneyUnit.Satoshi),
                                    BlockHeight = transaction.SpendingDetails.BlockHeight,
                                    Type = ContractTransactionItemType.ContractCall,
                                    Hash = transaction.SpendingDetails.TransactionId,
                                    To = txData.Value.ContractAddress.ToBase58Address(this.network)
                                });
                            }
                        }
                        else
                        {
                            // Create a record for every external payment sent
                            if (transaction.SpendingDetails.Payments != null)
                            {
                                foreach (PaymentDetails payment in transaction.SpendingDetails.Payments)
                                {
                                    transactionItems.Add(new ContractTransactionItem
                                    {
                                        Amount = payment.Amount.ToUnit(MoneyUnit.Satoshi),
                                        BlockHeight = transaction.SpendingDetails.BlockHeight,
                                        Type = ContractTransactionItemType.Send,
                                        Hash = transaction.SpendingDetails.TransactionId,
                                        To = payment.DestinationAddress
                                    });
                                }
                            }
                        }
                    }
                }

                return this.Json(transactionItems.OrderByDescending(x => x.BlockHeight ?? Int32.MaxValue).ThenBy(x => x.Hash.ToString()));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        [Route("create")]
        [HttpPost]
        public IActionResult Create([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = this.smartContractTransactionService.BuildCreateTx(request);

            if (!response.Success)
                return this.BadRequest(this.Json(response));

            Transaction transaction = this.network.CreateTransaction(response.Hex);
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return this.Json(response.TransactionId);
        }

        [Route("call")]
        [HttpPost]
        public IActionResult Call([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCallContractTransactionResponse response = this.smartContractTransactionService.BuildCallTx(request);
            if (!response.Success)
                return this.BadRequest(this.Json(response));

            Transaction transaction = this.network.CreateTransaction(response.Hex);
            this.walletManager.ProcessTransaction(transaction, null, null, false);
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            return this.Json(response);
        }

        [Route("send-transaction")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            if (!this.connectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                Transaction transaction = this.network.CreateTransaction(request.Hex);

                var model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (TxOut output in transaction.Outputs)
                {
                    bool isUnspendable = output.ScriptPubKey.IsUnspendable;

                    string address = this.GetAddressFromScriptPubKey(output);
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = address,
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());
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

        public static ContractTransactionItemType ReceivedTransactionType(TransactionData transaction)
        {
            bool isCoinBase = transaction.IsCoinBase.HasValue && transaction.IsCoinBase.Value;

            bool isMiningReward = isCoinBase && transaction.Index == 0;

            bool isGasRefund = isCoinBase && transaction.Index != 0;

            if (isGasRefund)
                return ContractTransactionItemType.GasRefund;

            if (isMiningReward)
                return ContractTransactionItemType.Staked;

            return ContractTransactionItemType.Received;
        }

        /// <summary>
        /// Retrieves a string that represents the receiving address for an output.For smart contract transactions,
        /// returns the opcode that was sent i.e.OP_CALL or OP_CREATE
        /// </summary>
        private string GetAddressFromScriptPubKey(TxOut output)
        {
            if (output.ScriptPubKey.IsSmartContractExec())
                return output.ScriptPubKey.ToOps().First().Code.ToString();

            if (!output.ScriptPubKey.IsUnspendable)
                return output.ScriptPubKey.GetDestinationAddress(this.network).ToString();

            return null;
        }
    }
}