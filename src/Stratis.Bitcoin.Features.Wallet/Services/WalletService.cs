using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Wallet.Services
{
    public class WalletService : IWalletService
    {
        private const int MaxHistoryItemsPerAccount = 1000;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly Network network;
        private readonly ChainIndexer chainIndexer;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly CoinType coinType;
        private readonly ILogger logger;

        public WalletService(ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IConsensusManager consensusManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.walletManager = walletManager;
            this.consensusManager = consensusManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.chainIndexer = chainIndexer;
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task<IEnumerable<string>> GetWalletNames(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => this.walletManager.GetWalletsNames(), cancellationToken);
        }

        public async Task<string> CreateWallet(WalletCreationRequest request, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Mnemonic requestMnemonic =
                        string.IsNullOrEmpty(request.Mnemonic) ? null : new Mnemonic(request.Mnemonic);

                    (_, Mnemonic mnemonic) = this.walletManager.CreateWallet(request.Password, request.Name,
                        request.Passphrase, mnemonic: requestMnemonic);

                    return mnemonic.ToString();
                }
                catch (WalletException e)
                {
                    // indicates that this wallet already exists
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    throw new FeatureException(HttpStatusCode.Conflict, e.Message, e.ToString());
                }
                catch (NotSupportedException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    throw new FeatureException(HttpStatusCode.BadRequest,
                        "There was a problem creating a wallet.", e.ToString());
                }
            }, cancellationToken);
        }

        public async Task<AddressBalanceModel> GetReceivedByAddress(string address,
            CancellationToken cancellationToken = default)
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
                    ChainTip = this.consensusManager.HeaderTip,
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
                IEnumerable<AccountHistory> accountsHistory =
                    this.walletManager.GetHistory(request.WalletName, request.AccountName);

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
                        // A staking transaction spends some of our inputs into 2 outputs or more, paid to the same address.
                        if (transaction.SpendingDetails?.IsCoinStake != null &&
                            transaction.SpendingDetails.IsCoinStake.Value)
                        {
                            // If another input has already triggered the building of this history item, we need to remove the amount of this input from the Amount.
                            // This will only ever happen when there are multiple inputs in a CoinStake. StratisX does this in certain situations.
                            if (uniqueProcessedTxIds.Contains(transaction.SpendingDetails.TransactionId))
                            {
                                TransactionItemModel existingStakeItem =
                                    transactionItems.Last(x => x.Id == transaction.SpendingDetails.TransactionId);
                                existingStakeItem.Amount -= transaction.Amount;
                            }
                            else
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
                                        ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                        BlockIndex = transaction.SpendingDetails.BlockIndex
                                    };

                                    transactionItems.Add(stakingItem);
                                    uniqueProcessedTxIds.Add(stakingItem.Id);
                                }
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
                    { Amount = sg.Key.ToDecimal(MoneyUnit.BTC), Count = sg.Count() })
                    .ToList();

                // This is number of UTXO originating from the same transaction
                // WalletInputsPerTransaction = 2000 and Count = 1; would be the result of one split coin operation into 2000 UTXOs
                model.UtxoPerTransaction = spendableTransactions
                    .GroupBy(s => s.Transaction.Id)
                    .GroupBy(sg => sg.Count())
                    .OrderByDescending(sgg => sgg.Count())
                    .Select(utxo => new UtxoPerTransactionModel
                    { WalletInputsPerTransaction = utxo.Key, Count = utxo.Count() })
                    .ToList();

                model.UtxoPerBlock = spendableTransactions
                    .GroupBy(s => s.Transaction.BlockHeight)
                    .GroupBy(sg => sg.Count())
                    .OrderByDescending(sgg => sgg.Count())
                    .Select(utxo => new UtxoPerBlockModel { WalletInputsPerBlock = utxo.Key, Count = utxo.Count() })
                    .ToList();

                return model;
            }, cancellationToken);
        }

        public async Task<WalletSendTransactionModel> SplitCoins(SplitCoinsRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var walletReference = new WalletAccountReference(request.WalletName, request.AccountName);
                HdAddress address = this.walletManager.GetUnusedAddress(walletReference);

                Money totalAmount = request.TotalAmountToSplit;
                Money singleUtxoAmount = totalAmount / request.UtxosCount;

                var recipients = new List<Recipient>(request.UtxosCount);
                for (int i = 0; i < request.UtxosCount; i++)
                    recipients.Add(new Recipient { ScriptPubKey = address.ScriptPubKey, Amount = singleUtxoAmount });

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = walletReference,
                    MinConfirmations = 1,
                    Shuffle = true,
                    WalletPassword = request.WalletPassword,
                    Recipients = recipients,
                    Time = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()
                };

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                return this.SendTransaction(new SendTransactionRequest(transactionResult.ToHex()),
                    CancellationToken.None);
            }, cancellationToken);
        }

        public async Task<WalletSendTransactionModel> SendTransaction(SendTransactionRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                if (!this.connectionManager.ConnectedPeers.Any())
                {
                    this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");

                    throw new FeatureException(HttpStatusCode.Forbidden,
                        "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
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

                    throw new FeatureException(HttpStatusCode.BadRequest,
                        transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                }

                return model;
            }, cancellationToken);
        }

        public async Task<IEnumerable<RemovedTransactionModel>> RemoveTransactions(RemoveTransactionsModel request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = this.walletManager.RemoveAllTransactions(request.WalletName);
                }
                else if (request.FromDate != default)
                {
                    result = this.walletManager.RemoveTransactionsFromDate(request.WalletName, request.FromDate);
                }
                else if (request.TransactionsIds != null)
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = this.walletManager.RemoveTransactionsByIds(request.WalletName, ids);
                }
                else
                {
                    throw new WalletException("A filter specifying what transactions to remove must be set.");
                }

                // If the user chose to resync the wallet after removing transactions.
                if (result.Any() && request.ReSync)
                {
                    // From the list of removed transactions, check which one is the oldest and retrieve the block right before that time.
                    DateTimeOffset earliestDate = result.Min(r => r.creationTime);
                    ChainedHeader chainedHeader =
                        this.chainIndexer.GetHeader(this.chainIndexer.GetHeightAtTime(earliestDate.DateTime));

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1, request.WalletName);
                }

                return result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });
            }, cancellationToken);
        }

        public async Task<AddressesModel> GetAllAddresses(GetAllAddressesModel request,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccount(request.AccountName);
                if (account == null)
                    throw new WalletException($"No account with the name '{request.AccountName}' could be found.");

                var accRef = new WalletAccountReference(request.WalletName, request.AccountName);

                var unusedNonChange = this.walletManager.GetUnusedAddresses(accRef, false)
                    .Select(a => (address: a, isUsed: false, isChange: false, confirmed: Money.Zero, total: Money.Zero))
                    .ToList();
                var unusedChange = this.walletManager.GetUnusedAddresses(accRef, true)
                    .Select(a => (address: a, isUsed: false, isChange: true, confirmed: Money.Zero, total: Money.Zero))
                    .ToList();
                var usedNonChange = this.walletManager.GetUsedAddresses(accRef, false)
                    .Select(a => (a.address, isUsed: true, isChange: false, a.confirmed, a.total)).ToList();
                var usedChange = this.walletManager.GetUsedAddresses(accRef, true)
                    .Select(a => (a.address, isUsed: true, isChange: true, a.confirmed, a.total)).ToList();

                return new AddressesModel()
                {
                    Addresses = unusedNonChange
                        .Concat(unusedChange)
                        .Concat(usedNonChange)
                        .Concat(usedChange)
                        .Select(a => new AddressModel
                        {
                            Address = a.address.Address,
                            IsUsed = a.isUsed,
                            IsChange = a.isChange,
                            AmountConfirmed = a.confirmed,
                            AmountUnconfirmed = a.total - a.confirmed
                        })
                };
            }, cancellationToken);
        }

        public async Task<WalletBuildTransactionModel> BuildTransaction(BuildTransactionRequest request,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var recipients = request.Recipients.Select(recipientModel => new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                    Amount = recipientModel.Amount
                }).ToList();

                // If specified, get the change address, which must already exist in the wallet.
                HdAddress changeAddress = null;
                if (!string.IsNullOrWhiteSpace(request.ChangeAddress))
                {
                    Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                    HdAccount account = wallet.GetAccount(request.AccountName);
                    if (account == null)
                    {
                        throw new FeatureException(HttpStatusCode.BadRequest, "Account not found.",
                            $"No account with the name '{request.AccountName}' could be found in wallet {wallet.Name}.");
                    }

                    changeAddress = account.GetCombinedAddresses()
                        .FirstOrDefault(x => x.Address == request.ChangeAddress);

                    if (changeAddress == null)
                    {
                        throw new FeatureException(HttpStatusCode.BadRequest, "Change address not found.",
                            $"No changed address '{request.ChangeAddress}' could be found in wallet {wallet.Name}.");
                    }
                }

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                    TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Shuffle = request.ShuffleOutputs ??
                              true, // We shuffle transaction outputs by default as it's better for anonymity.
                    OpReturnData = request.OpReturnData,
                    OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount)
                        ? null
                        : Money.Parse(request.OpReturnAmount),
                    WalletPassword = request.Password,
                    SelectedInputs = request.Outpoints
                        ?.Select(u => new OutPoint(uint256.Parse(u.TransactionId), u.Index)).ToList(),
                    AllowOtherInputs = false,
                    Recipients = recipients,
                    ChangeAddress = changeAddress
                };

                if (!string.IsNullOrEmpty(request.FeeType))
                {
                    context.FeeType = FeeParser.Parse(request.FeeType);
                }

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                return new WalletBuildTransactionModel
                {
                    Hex = transactionResult.ToHex(),
                    Fee = context.TransactionFee,
                    TransactionId = transactionResult.GetHash()
                };
            }, cancellationToken);
        }

        public async Task<Money> GetTransactionFeeEstimate(TxFeeEstimateRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var recipients = request.Recipients.Select(recipientModel => new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                    Amount = recipientModel.Amount
                }).ToList();

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Recipients = recipients,
                    OpReturnData = request.OpReturnData,
                    OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount)
                        ? null
                        : Money.Parse(request.OpReturnAmount),
                    Sign = false
                };

                return this.walletTransactionHandler.EstimateFee(context);
            }, cancellationToken);
        }

        public async Task RecoverViaExtPubKey(WalletExtPubRecoveryRequest request, CancellationToken token)
        {
            await Task.Run(() =>
            {
                try
                {
                    string accountExtPubKey =
                        this.network.IsBitcoin()
                            ? request.ExtPubKey
                            : LegacyExtPubKeyConverter.ConvertIfInLegacyStratisFormat(request.ExtPubKey, this.network);

                    this.walletManager.RecoverWallet(request.Name, ExtPubKey.Parse(accountExtPubKey),
                        request.AccountIndex,
                        request.CreationDate, null);
                }
                catch (WalletException e)
                {
                    // Wallet already exists.
                    throw new FeatureException(HttpStatusCode.Conflict, e.Message, e.ToString());
                }
                catch (FileNotFoundException e)
                {
                    // Wallet does not exist.
                    throw new FeatureException(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
                }
            }, token);
        }

        public async Task RecoverWallet(WalletRecoveryRequest request, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    this.walletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic,
                        request.CreationDate, passphrase: request.Passphrase);
                }
                catch (WalletException e)
                {
                    // indicates that this wallet already exists
                    throw new FeatureException(HttpStatusCode.Conflict, e.Message, e.ToString());
                }
                catch (FileNotFoundException e)
                {
                    // indicates that this wallet does not exist
                    throw new FeatureException(HttpStatusCode.NotFound, "Wallet not found.", e.ToString());
                }
            }, cancellationToken);
        }

        public async Task LoadWallet(WalletLoadRequest request, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                try
                {
                    this.walletManager.LoadWallet(request.Password, request.Name);
                }
                catch (FileNotFoundException e)
                {
                    throw new FeatureException(HttpStatusCode.NotFound,
                        "This wallet was not found at the specified location.", e.ToString());
                }
                catch (WalletException e)
                {
                    throw new FeatureException(HttpStatusCode.NotFound,
                        "This wallet was not found at the specified location.", e.ToString());
                }
                catch (SecurityException e)
                {
                    // indicates that the password is wrong
                    throw new FeatureException(HttpStatusCode.Forbidden,
                        "Wrong password, please try again.",
                        e.ToString());
                }
            }, cancellationToken);
        }

        public async Task<MaxSpendableAmountModel> GetMaximumSpendableBalance(WalletMaximumBalanceRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                (Money maximumSpendableAmount, Money fee) = this.walletTransactionHandler.GetMaximumSpendableAmount(
                    new WalletAccountReference(request.WalletName, request.AccountName),
                    FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);

                return new MaxSpendableAmountModel
                {
                    MaxSpendableAmount = maximumSpendableAmount,
                    Fee = fee
                };
            }, cancellationToken);
        }

        public async Task<SpendableTransactionsModel> GetSpendableTransactions(SpendableTransactionsRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                IEnumerable<UnspentOutputReference> spendableTransactions =
                    this.walletManager.GetSpendableTransactionsInAccount(
                        new WalletAccountReference(request.WalletName, request.AccountName), request.MinConfirmations);

                return new SpendableTransactionsModel
                {
                    SpendableTransactions = spendableTransactions.Select(st => new SpendableTransactionModel
                    {
                        Id = st.Transaction.Id,
                        Amount = st.Transaction.Amount,
                        Address = st.Address.Address,
                        Index = st.Transaction.Index,
                        IsChange = st.Address.IsChangeAddress(),
                        CreationTime = st.Transaction.CreationTime,
                        Confirmations = st.Confirmations
                    }).ToList()
                };
            }, cancellationToken);
        }

        public async Task<DistributeUtxoModel> DistributeUtxos(DistributeUtxosRequest request,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var model = new DistributeUtxoModel()
                {
                    WalletName = request.WalletName,
                    UseUniqueAddressPerUtxo = request.UseUniqueAddressPerUtxo,
                    UtxosCount = request.UtxosCount,
                    UtxoPerTransaction = request.UtxoPerTransaction,
                    TimestampDifferenceBetweenTransactions = request.TimestampDifferenceBetweenTransactions,
                    MinConfirmations = request.MinConfirmations,
                    DryRun = request.DryRun
                };

                var walletReference = new WalletAccountReference(request.WalletName, request.AccountName);

                Wallet wallet = this.walletManager.GetWallet(request.WalletName);
                HdAccount account = wallet.GetAccount(request.AccountName);

                var addresses = new List<HdAddress>();

                if (request.ReuseAddresses)
                {
                    addresses = this.walletManager.GetUnusedAddresses(walletReference,
                        request.UseUniqueAddressPerUtxo ? request.UtxosCount : 1, request.UseChangeAddresses).ToList();
                }
                else if (request.UseChangeAddresses)
                {
                    addresses = account.InternalAddresses.Take(request.UseUniqueAddressPerUtxo ? request.UtxosCount : 1)
                        .ToList();
                }
                else if (!request.UseChangeAddresses)
                {
                    addresses = account.ExternalAddresses.Take(request.UseUniqueAddressPerUtxo ? request.UtxosCount : 1)
                        .ToList();
                }

                IEnumerable<UnspentOutputReference> spendableTransactions =
                    this.walletManager.GetSpendableTransactionsInAccount(
                        new WalletAccountReference(request.WalletName, request.AccountName), request.MinConfirmations);

                if (request.Outpoints != null && request.Outpoints.Any())
                {
                    var selectedUnspentOutputReferenceList = new List<UnspentOutputReference>();
                    foreach (UnspentOutputReference unspentOutputReference in spendableTransactions)
                    {
                        if (request.Outpoints.Any(o =>
                            o.TransactionId == unspentOutputReference.Transaction.Id.ToString() &&
                            o.Index == unspentOutputReference.Transaction.Index))
                        {
                            selectedUnspentOutputReferenceList.Add(unspentOutputReference);
                        }
                    }

                    spendableTransactions = selectedUnspentOutputReferenceList;
                }

                int totalOutpointCount = spendableTransactions.Count();
                int calculatedTransactionCount = request.UtxosCount / request.UtxoPerTransaction;
                int inputsPerTransaction = totalOutpointCount / calculatedTransactionCount;

                if (calculatedTransactionCount > totalOutpointCount)
                {
                    this.logger.LogError(
                        $"You have requested to create {calculatedTransactionCount} transactions but there are only {totalOutpointCount} UTXOs in the wallet. Number of transactions which could be created has to be lower than total number of UTXOs in the wallet. If higher number of transactions is required please first distibute funds to create larget set of UTXO and retry this operation.");
                    throw new FeatureException(HttpStatusCode.BadRequest, "Invalid parameters", "Invalid parameters");
                }

                var recipients = new List<Recipient>(request.UtxosCount);
                int addressIndex = 0;
                var transactionList = new List<Transaction>();

                for (int i = 0; i < request.UtxosCount; i++)
                {
                    recipients.Add(new Recipient { ScriptPubKey = addresses[addressIndex].ScriptPubKey });

                    if (request.UseUniqueAddressPerUtxo)
                        addressIndex++;

                    if ((i + 1) % request.UtxoPerTransaction == 0 || i == request.UtxosCount - 1)
                    {
                        var transactionTransferAmount = new Money(0);
                        var inputs = new List<OutPoint>();

                        foreach (UnspentOutputReference unspentOutputReference in spendableTransactions
                            .Skip(transactionList.Count * inputsPerTransaction).Take(inputsPerTransaction))
                        {
                            inputs.Add(new OutPoint(unspentOutputReference.Transaction.Id,
                                unspentOutputReference.Transaction.Index));
                            transactionTransferAmount += unspentOutputReference.Transaction.Amount;
                        }

                        // Add any remaining UTXOs to the last transaction.
                        if (i == request.UtxosCount - 1)
                        {
                            foreach (UnspentOutputReference unspentOutputReference in spendableTransactions.Skip(
                                (transactionList.Count + 1) * inputsPerTransaction))
                            {
                                inputs.Add(new OutPoint(unspentOutputReference.Transaction.Id,
                                    unspentOutputReference.Transaction.Index));
                                transactionTransferAmount += unspentOutputReference.Transaction.Amount;
                            }
                        }

                        // For the purpose of fee estimation use the transfer amount as if the fee were network.MinTxFee.
                        Money transferAmount = (transactionTransferAmount) / recipients.Count;
                        recipients.ForEach(r => r.Amount = transferAmount);

                        var context = new TransactionBuildContext(this.network)
                        {
                            AccountReference = walletReference,
                            Shuffle = false,
                            WalletPassword = request.WalletPassword,
                            Recipients = recipients,
                            Time = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp() +
                                   (uint)request.TimestampDifferenceBetweenTransactions,
                            AllowOtherInputs = false,
                            SelectedInputs = inputs,
                            FeeType = FeeType.Low
                        };

                        // Set the amount once we know how much the transfer will cost.
                        Money transactionFee;
                        try
                        {
                            Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);

                            // Due to how the code works the line below is probably never used.
                            var transactionSize = transaction.GetSerializedSize();
                            transactionFee = new FeeRate(this.network.MinTxFee).GetFee(transactionSize);
                        }
                        catch (NotEnoughFundsException ex)
                        {
                            // This remains the best approach for estimating transaction fees.
                            transactionFee = (Money)ex.Missing;
                        }

                        if (transactionFee < this.network.MinTxFee)
                            transactionFee = new Money(this.network.MinTxFee);

                        transferAmount = (transactionTransferAmount - transactionFee) / recipients.Count;
                        recipients.ForEach(r => r.Amount = transferAmount);

                        context = new TransactionBuildContext(this.network)
                        {
                            AccountReference = walletReference,
                            Shuffle = false,
                            WalletPassword = request.WalletPassword,
                            Recipients = recipients,
                            Time = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp() +
                                   (uint)request.TimestampDifferenceBetweenTransactions,
                            AllowOtherInputs = false,
                            SelectedInputs = inputs,
                            TransactionFee = transactionFee
                        };

                        Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);
                        transactionList.Add(transactionResult);
                        recipients = new List<Recipient>();
                    }
                }

                foreach (Transaction transaction in transactionList)
                {
                    var modelItem = new WalletSendTransactionModel
                    {
                        TransactionId = transaction.GetHash(),
                        Outputs = new List<TransactionOutputModel>()
                    };

                    foreach (TxOut output in transaction.Outputs)
                    {
                        bool isUnspendable = output.ScriptPubKey.IsUnspendable;
                        modelItem.Outputs.Add(new TransactionOutputModel
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

                    model.WalletSendTransaction.Add(modelItem);

                    if (!request.DryRun)
                    {
                        this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                        TransactionBroadcastEntry transactionBroadCastEntry =
                            this.broadcasterManager.GetTransaction(transaction.GetHash());

                        if (transactionBroadCastEntry.State == State.CantBroadcast)
                        {
                            this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                            throw new FeatureException(HttpStatusCode.BadRequest,
                                transactionBroadCastEntry.ErrorMessage,
                                "Transaction Exception");
                        }
                    }
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