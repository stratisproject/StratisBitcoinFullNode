namespace Stratis.Bitcoin.Features.Wallet.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Broadcasting;
    using Connection;
    using Helpers;
    using Interfaces;
    using Microsoft.Extensions.Logging;
    using Models;
    using NBitcoin;
    using Utilities;
    
    public class WalletService : IWalletService
    {
        private const int MaxHistoryItemsPerAccount = 1000;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IConnectionManager connectionManager;
        private readonly Network network;
        private readonly ChainIndexer chainIndexer;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly CoinType coinType;
        private readonly ILogger logger;

        public WalletService(ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.connectionManager = connectionManager;
            this.network = network;
            this.chainIndexer = chainIndexer;
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
            this.coinType = (CoinType) network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task<AddressBalanceModel> GetReceivedByAddress(string address,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                AddressBalance balanceResult = this.walletManager.GetAddressBalance(address);
                return new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed,
                    SpendableAmount = balanceResult.SpendableAmount
                };
            }, cancellationToken);
        }

        public async Task<WalletGeneralInfoModel> GetWalletGeneralInfo(string walletName,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                Wallet wallet = this.walletManager.GetWallet(walletName);

                return new WalletGeneralInfoModel
                {
                    WalletName = wallet.Name,
                    Network = wallet.Network,
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.AccountsRoot.Single().LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chainIndexer.Tip.Height,
                    IsChainSynced = this.chainIndexer.IsDownloaded(),
                    IsDecrypted = true
                };
            }, cancellationToken);
        }

        public async Task<WalletBalanceModel> GetBalance(
            string walletName, string accountName, bool includeBalanceByAddress = false,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var model = new WalletBalanceModel();

                IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(walletName, accountName).ToList();

                if (accountName != null && !balances.Any())
                    throw new Exception($"No account with the name '{accountName}' could be found.");

                foreach (AccountBalance balance in balances)
                {
                    HdAccount account = balance.Account;
                    model.AccountsBalances.Add(new AccountBalanceModel
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = balance.AmountConfirmed,
                        AmountUnconfirmed = balance.AmountUnconfirmed,
                        SpendableAmount = balance.SpendableAmount,
                        Addresses = includeBalanceByAddress
                            ? account.GetCombinedAddresses().Select(address =>
                            {
                                (Money confirmedAmount, Money unConfirmedAmount) = address.GetBalances();
                                return new AddressModel
                                {
                                    Address = address.Address,
                                    IsUsed = address.Transactions.Any(),
                                    IsChange = address.IsChangeAddress(),
                                    AmountConfirmed = confirmedAmount,
                                    AmountUnconfirmed = unConfirmedAmount
                                };
                            })
                            : null
                    });
                }

                return model;
            }, cancellationToken);
        }

        public async Task<WalletHistoryModel> GetHistory(WalletHistoryRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager
                    .GetHistory(request.WalletName,
                        request.AccountName,
                        request.PrevOutputTxTime,
                        request.PrevOutputIndex,
                        request.Take ?? int.MaxValue);

                foreach (AccountHistory accountHistory in accountsHistory)
                {
                    var transactionItems = new List<TransactionItemModel>();
                    var uniqueProcessedTxIds = new HashSet<uint256>();

                    IEnumerable<FlatHistory> query = accountHistory.History;

                    if (!string.IsNullOrEmpty(request.Address))
                    {
                        query = query.Where(x => x.Address.Address == request.Address);
                    }

                    // Sorting the history items by descending dates. That includes received and sent dates.
                    List<FlatHistory> items = query
                        .OrderBy(o => o.Transaction.IsConfirmed() ? 1 : 0)
                        .ThenByDescending(
                            o => o.Transaction.SpendingDetails?.CreationTime ?? o.Transaction.CreationTime)
                        .ToList();

                    var lookup = items.ToLookup(i => i.Transaction.Id, i => i);

                    // Represents a sublist containing only the transactions that have already been spent.
                    var spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null)
                        .ToLookup(s => s.Transaction.SpendingDetails.TransactionId, s => s);

                    // Represents a sublist of 'change' transactions.
                    // NB: Not currently used
                    // List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                    // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                    // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                    foreach (FlatHistory item in items.Where(t =>
                        !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && t.Transaction.IsSpent())))
                    {
                        // Count only unique transactions and limit it to MaxHistoryItemsPerAccount.
                        int processedTransactions = uniqueProcessedTxIds.Count;
                        if (processedTransactions >= MaxHistoryItemsPerAccount)
                        {
                            break;
                        }

                        TransactionData transaction = item.Transaction;
                        HdAddress address = item.Address;

                        // First we look for staking transaction as they require special attention.
                        // A staking transaction spends one of our inputs into 2 outputs or more, paid to the same address.
                        if (transaction.SpendingDetails?.IsCoinStake != null &&
                            transaction.SpendingDetails.IsCoinStake.Value)
                        {
                            // We look for the output(s) related to our spending input.
                            List<FlatHistory> relatedOutputs = lookup.Contains(transaction.Id)
                                ? lookup[transaction.SpendingDetails.TransactionId].Where(h =>
                                    h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList()
                                : null;

                            if (false != relatedOutputs?.Any())
                            {
                                // Add staking transaction details.
                                // The staked amount is calculated as the difference between the sum of the outputs and the input and should normally be equal to 1.
                                var stakingItem = new TransactionItemModel
                                {
                                    Type = TransactionItemType.Staked,
                                    ToAddress = address.Address,
                                    Amount = relatedOutputs.Sum(o => o.Transaction.Amount) - transaction.Amount,
                                    Id = transaction.SpendingDetails.TransactionId,
                                    Timestamp = transaction.SpendingDetails.CreationTime,
                                    TxOutputIndex = transaction.Index,
                                    ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                    BlockIndex = transaction.SpendingDetails.BlockIndex
                                };

                                transactionItems.Add(stakingItem);
                                uniqueProcessedTxIds.Add(stakingItem.Id);
                            }

                            // No need for further processing if the transaction itself is the output of a staking transaction.
                            if (transaction.IsCoinStake == true)
                            {
                                continue;
                            }
                        }

                        // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                        if (transaction.SpendingDetails != null && transaction.SpendingDetails.IsCoinStake != true)
                        {
                            // Create a record for a 'send' transaction.
                            uint256 spendingTransactionId = transaction.SpendingDetails.TransactionId;
                            var sentItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Send,
                                Id = spendingTransactionId,
                                Timestamp = transaction.SpendingDetails.CreationTime,
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                BlockIndex = transaction.SpendingDetails.BlockIndex,
                                Amount = Money.Zero
                            };

                            // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
                            if (transaction.SpendingDetails.Payments != null)
                            {
                                sentItem.Payments = new List<PaymentDetailModel>();
                                foreach (PaymentDetails payment in transaction.SpendingDetails.Payments)
                                {
                                    sentItem.Payments.Add(new PaymentDetailModel
                                    {
                                        DestinationAddress = payment.DestinationAddress,
                                        Amount = payment.Amount
                                    });

                                    sentItem.Amount += payment.Amount;
                                }
                            }

                            Money changeAmount = transaction.SpendingDetails.Change.Sum(d => d.Amount);

                            // Get the change address for this spending transaction.
                            // NB: Not currently used
                            // FlatHistory changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                            // Find all the spending details containing the spending transaction id and aggregate the sums.
                            // This is our best shot at finding the total value of inputs for this transaction.
                            var inputsAmount = new Money(spendingDetails.Contains(spendingTransactionId)
                                ? spendingDetails[spendingTransactionId].Sum(t => t.Transaction.Amount)
                                : 0);

                            // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                            sentItem.Fee = inputsAmount - sentItem.Amount - changeAmount;

                            // Mined/staked coins add more coins to the total out.
                            // That makes the fee negative. If that's the case ignore the fee.
                            if (sentItem.Fee < 0)
                                sentItem.Fee = 0;

                            transactionItems.Add(sentItem);
                            uniqueProcessedTxIds.Add(sentItem.Id);
                        }

                        // We don't show in history transactions that are outputs of staking transactions.
                        if (transaction.IsCoinStake != null && transaction.IsCoinStake.Value &&
                            transaction.SpendingDetails == null)
                        {
                            continue;
                        }

                        // Create a record for a 'receive' transaction.
                        if (!address.IsChangeAddress())
                        {
                            // First check if we already have a similar transaction output, in which case we just sum up the amounts
                            TransactionItemModel existingReceivedItem =
                                this.FindSimilarReceivedTransactionOutput(transactionItems, transaction);

                            if (existingReceivedItem == null)
                            {
                                // Add incoming fund transaction details.
                                var receivedItem = new TransactionItemModel
                                {
                                    Type = TransactionItemType.Received,
                                    ToAddress = address.Address,
                                    Amount = transaction.Amount,
                                    Id = transaction.Id,
                                    Timestamp = transaction.CreationTime,
                                    ConfirmedInBlock = transaction.BlockHeight,
                                    BlockIndex = transaction.BlockIndex
                                };

                                transactionItems.Add(receivedItem);
                                uniqueProcessedTxIds.Add(receivedItem.Id);
                            }
                            else
                            {
                                existingReceivedItem.Amount += transaction.Amount;
                            }
                        }
                    }

                    transactionItems = transactionItems.Distinct(new SentTransactionItemModelComparer()).Select(e => e)
                        .ToList();

                    // Sort and filter the history items.
                    List<TransactionItemModel> itemsToInclude = transactionItems.OrderByDescending(t => t.Timestamp)
                        .Where(x => string.IsNullOrEmpty(request.SearchQuery) ||
                                    (x.Id.ToString() == request.SearchQuery || x.ToAddress == request.SearchQuery ||
                                     x.Payments.Any(p => p.DestinationAddress == request.SearchQuery)))
                        .Skip(request.Skip ?? 0)
                        .Take(request.Take ?? transactionItems.Count)
                        .ToList();

                    model.AccountsHistoryModel.Add(new AccountHistoryModel
                    {
                        TransactionsHistory = itemsToInclude,
                        Name = accountHistory.Account.Name,
                        CoinType = this.coinType,
                        HdPath = accountHistory.Account.HdPath
                    });
                }

                return model;
            }, cancellationToken);
        }

        public async Task<WalletStatsModel> GetWalletStats(WalletStatsRequest request,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var model = new WalletStatsModel
                {
                    WalletName = request.WalletName
                };

                IEnumerable<UnspentOutputReference> spendableTransactions =
                    this.walletManager.GetSpendableTransactionsInAccount(
                            new WalletAccountReference(request.WalletName, request.AccountName),
                            request.MinConfirmations)
                        .ToList();

                model.TotalUtxoCount = spendableTransactions.Count();
                model.UniqueTransactionCount =
                    spendableTransactions.GroupBy(s => s.Transaction.Id).Select(s => s.Key).Count();
                model.UniqueBlockCount = spendableTransactions.GroupBy(s => s.Transaction.BlockHeight)
                    .Select(s => s.Key).Count();
                model.FinalizedTransactions =
                    spendableTransactions.Count(s => s.Confirmations >= this.network.Consensus.MaxReorgLength);

                if (!request.Verbose)
                {
                    return model;
                }

                model.UtxoAmounts = spendableTransactions
                    .GroupBy(s => s.Transaction.Amount)
                    .OrderByDescending(sg => sg.Count())
                    .Select(sg => new UtxoAmountModel
                        {Amount = sg.Key.ToDecimal(MoneyUnit.BTC), Count = sg.Count()})
                    .ToList();

                // This is number of UTXO originating from the same transaction
                // WalletInputsPerTransaction = 2000 and Count = 1; would be the result of one split coin operation into 2000 UTXOs
                model.UtxoPerTransaction = spendableTransactions
                    .GroupBy(s => s.Transaction.Id)
                    .GroupBy(sg => sg.Count())
                    .OrderByDescending(sgg => sgg.Count())
                    .Select(utxo => new UtxoPerTransactionModel
                        {WalletInputsPerTransaction = utxo.Key, Count = utxo.Count()})
                    .ToList();

                model.UtxoPerBlock = spendableTransactions
                    .GroupBy(s => s.Transaction.BlockHeight)
                    .GroupBy(sg => sg.Count())
                    .OrderByDescending(sgg => sgg.Count())
                    .Select(utxo => new UtxoPerBlockModel {WalletInputsPerBlock = utxo.Key, Count = utxo.Count()})
                    .ToList();


                return model;
            });
        }

        public Task<WalletSendTransactionModel> SplitCoins(SplitCoinsRequest request, CancellationToken cancellationToken)
        {
            var walletReference = new WalletAccountReference(request.WalletName, request.AccountName);
            HdAddress address = this.walletManager.GetUnusedAddress(walletReference);

            Money totalAmount = request.TotalAmountToSplit;
            Money singleUtxoAmount = totalAmount / request.UtxosCount;

            var recipients = new List<Recipient>(request.UtxosCount);
            for (int i = 0; i < request.UtxosCount; i++)
                recipients.Add(new Recipient {ScriptPubKey = address.ScriptPubKey, Amount = singleUtxoAmount});

            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = walletReference,
                MinConfirmations = 1,
                Shuffle = true,
                WalletPassword = request.WalletPassword,
                Recipients = recipients,
                Time = (uint) this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()
            };

            Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

            return this.SendTransaction(new SendTransactionRequest(transactionResult.ToHex()), CancellationToken.None);
        }

        private async Task<WalletSendTransactionModel> SendTransaction(SendTransactionRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
                {
                    if (!this.connectionManager.ConnectedPeers.Any())
                    {
                        this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");

                        // TODO: Consider how to do this
//                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden,
//                    "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
                    }


                    Transaction transaction = this.network.CreateTransaction(request.Hex);

                    var model = new WalletSendTransactionModel
                    {
                        TransactionId = transaction.GetHash(),
                        Outputs = new List<TransactionOutputModel>()
                    };

                    foreach (TxOut output in transaction.Outputs)
                    {
                        bool isUnspendable = output.ScriptPubKey.IsUnspendable;
                        model.Outputs.Add(new TransactionOutputModel
                        {
                            Address = isUnspendable
                                ? null
                                : output.ScriptPubKey.GetDestinationAddress(this.network)?.ToString(),
                            Amount = output.Value,
                            OpReturnData = isUnspendable
                                ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData)
                                : null
                        });
                    }

                    this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                    TransactionBroadcastEntry transactionBroadCastEntry =
                        this.broadcasterManager.GetTransaction(transaction.GetHash());

                    if (transactionBroadCastEntry.State == State.CantBroadcast)
                    {
                        this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                        // TODO: Consider how to do this
//                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
//                        transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                    }

                    return model;
                }, cancellationToken);
        }


        private TransactionItemModel FindSimilarReceivedTransactionOutput(List<TransactionItemModel> items,
            TransactionData transaction)
        {
            return items.FirstOrDefault(i => i.Id == transaction.Id &&
                                             i.Type == TransactionItemType.Received &&
                                             i.ConfirmedInBlock == transaction.BlockHeight);
        }
    }
}