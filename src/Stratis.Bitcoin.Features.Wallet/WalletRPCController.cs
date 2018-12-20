using System;
using System.Linq;
using System.Security;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
    {
        // <summary>As per RPC method definition this should be the max allowable expiry duration.</summary>
        private const int maxDurationInSeconds = 1073741824;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Full node.</summary>
        private readonly IFullNode fullNode;

        /// <summary>Wallet broadcast manager.</summary>
        private readonly IBroadcasterManager broadcasterManager;

        /// <summary>Wallet manager.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>Wallet transaction handler.</summary>
        private readonly IWalletTransactionHandler walletTransactionHandler;

        public WalletRPCController(IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IFullNode fullNode, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory, IConsensusManager consensusManager) : base(fullNode: fullNode, consensusManager: consensusManager)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.fullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("walletpassphrase")]
        [ActionDescription("Stores the wallet decryption key in memory for the indicated number of seconds. Issuing the walletpassphrase command while the wallet is already unlocked will set a new unlock time that overrides the old one.")]
        [NoTrace]
        public bool UnlockWallet(string passphrase, int timeout)
        {
            Guard.NotEmpty(passphrase, nameof(passphrase));

            WalletAccountReference account = this.GetAccount();

            try
            {
                this.walletManager.UnlockWallet(account, passphrase, timeout);
            }
            catch (SecurityException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, exception.Message);
            }
            return true; // NOTE: Have to return a value or else RPC middleware doesn't serialize properly.
        }

        [ActionName("walletlock")]
        [ActionDescription("Removes the wallet encryption key from memory, locking the wallet. After calling this method, you will need to call walletpassphrase again before being able to call any methods which require the wallet to be unlocked.")]
        public bool LockWallet()
        {
            WalletAccountReference account = this.GetAccount();
            this.walletTransactionHandler.ClearCachedSecret(account);
            return true; // NOTE: Have to return a value or else RPC middleware doesn't serialize properly.
        }

        [ActionName("sendtoaddress")]
        [ActionDescription("Sends money to an address. Requires wallet to be unlocked using walletpassphrase.")]
        public async Task<uint256> SendToAddressAsync(BitcoinAddress address, decimal amount, string commentTx, string commentDest)
        {
            WalletAccountReference account = this.GetAccount(); 
            TransactionBuildContext context = new TransactionBuildContext(this.fullNode.Network)
            {
                AccountReference = this.GetAccount(),
                Recipients = new [] {new Recipient { Amount = Money.Coins(amount), ScriptPubKey = address.ScriptPubKey } }.ToList()
            };

            try
            {
                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                await this.broadcasterManager.BroadcastTransactionAsync(transaction);

                uint256 hash = transaction.GetHash();
                return hash;
            }
            catch (SecurityException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED, exception.Message);
            }
            catch (WalletException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_WALLET_ERROR, exception.Message);
            }
        }

        /// <summary>
        /// Broadcasts a raw transaction from hex to local node and network.
        /// </summary>
        /// <param name="hex">Raw transaction in hex.</param>
        /// <returns>The transaction hash.</returns>
        [ActionName("sendrawtransaction")]
        [ActionDescription("Submits raw transaction (serialized, hex-encoded) to local node and network.")]
        public async Task<uint256> SendTransactionAsync(string hex)
        {
            Transaction transaction = this.fullNode.Network.CreateTransaction(hex);
            await this.broadcasterManager.BroadcastTransactionAsync(transaction);

            uint256 hash = transaction.GetHash();
            
            return hash;
        }
             
        /// <summary>
        /// RPC method that gets a new address for receiving payments.
        /// Uses the first wallet and account.
        /// </summary>
        /// <param name="account">Parameter is deprecated.</param>
        /// <param name="addressType">Address type, currently only 'legacy' is supported.</param>
        /// <returns>The new address.</returns>
        [ActionName("getnewaddress")]
        [ActionDescription("Returns a new wallet address for receiving payments.")]
        public NewAddressModel GetNewAddress(string account, string addressType)
        {
            if (!string.IsNullOrEmpty(account))
                throw new RPCServerException(RPCErrorCode.RPC_METHOD_DEPRECATED, "Use of 'account' parameter has been deprecated");

            if (!string.IsNullOrEmpty(addressType))
            {
                // Currently segwit and bech32 addresses are not supported.
                if (!addressType.Equals("legacy", StringComparison.InvariantCultureIgnoreCase))
                    throw new RPCServerException(RPCErrorCode.RPC_METHOD_NOT_FOUND, "Only address type 'legacy' is currently supported.");
            }
            HdAddress hdAddress = this.walletManager.GetUnusedAddress(this.GetAccount());
            string base58Address = hdAddress.Address;
            
            return new NewAddressModel(base58Address);
        }

        /// <summary>
        /// RPC method that returns the total available balance.
        /// The available balance is what the wallet considers currently spendable.
        /// 
        /// Uses the first wallet and account.
        /// </summary>
        /// <param name="accountName">Remains for backward compatibility. Must be excluded or set to "*" or "". Deprecated in latest bitcoin core (0.17.0).</param>
        /// <param name="minConfirmations">Only include transactions confirmed at least this many times. (default=0)</param>
        /// <returns>Total spendable balance of the wallet.</returns>
        [ActionName("getbalance")]
        [ActionDescription("Gets wallets spendable balance.")]
        public decimal GetBalance(string accountName, int minConfirmations=0)
        {
            if (!string.IsNullOrEmpty(accountName) && !accountName.Equals("*"))
                throw new RPCServerException(RPCErrorCode.RPC_METHOD_DEPRECATED, "Account has been deprecated, must be excluded or set to \"*\"");

            var account = this.GetAccount();

            Money balance = this.walletManager.GetSpendableTransactionsInAccount(account, minConfirmations).Sum(x => x.Transaction.Amount);
            return balance?.ToUnit(MoneyUnit.BTC) ?? 0;
        }

        [ActionName("getwalletinfo")]
        [ActionDescription("Provides information about the wallet.")]
        public GetWalletInfoModel GetWalletInfo()
        {
            var accountReference = this.GetAccount();
            var account = this.walletManager.GetAccounts(accountReference.WalletName)
                                            .Where(i => i.Name.Equals(accountReference.AccountName))
                                            .Single();

            (Money confirmedAmount, Money unconfirmedAmount) = account.GetSpendableAmount();

            var model = new GetWalletInfoModel
            {
                Balance = Money.Coins(GetBalance(string.Empty)),
                WalletName = accountReference.WalletName + ".wallet.json",
                WalletVersion = 1,
                UnConfirmedBalance = unconfirmedAmount,
                ImmatureBalance = Money.Coins(GetBalance(string.Empty, (int)this.FullNode.Network.Consensus.CoinbaseMaturity))
            };

            return model;
        }

        private int GetConformationCount(TransactionData transaction)
        {
            if (transaction.BlockHeight.HasValue)
            {
                var blockCount = this.ConsensusManager?.Tip.Height ?? -1; // TODO: This is available in FullNodeController, should refactor and reuse the logic.
                return blockCount - transaction.BlockHeight.Value;
            }

            return -1;
        }

        private WalletHistoryModel GetHistory(string walletName, string accountName, uint256 trxid)
        {
            var model = new WalletHistoryModel();

            // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
            IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistoryById(trxid, walletName, accountName).ToList();

            foreach (AccountHistory accountHistory in accountsHistory)
            {
                var transactionItems = new List<TransactionItemModel>();

                List<FlatHistory> items = accountHistory.History.OrderByDescending(o => o.Transaction.CreationTime).ToList();
                //items = string.IsNullOrEmpty(request.SearchQuery) ? items.Take(200).ToList() : items;

                // Represents a sublist containing only the transactions that have already been spent.
                List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && !t.Transaction.IsSpendable())).ToList();

                // Represents a sublist of 'change' transactions.
                List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                foreach (FlatHistory item in history)
                {
                    TransactionData transaction = item.Transaction;
                    HdAddress address = item.Address;

                    // We don't show in history transactions that are outputs of staking transactions.
                    if (transaction.IsCoinStake != null && transaction.IsCoinStake.Value && transaction.SpendingDetails == null)
                    {
                        continue;
                    }

                    // First we look for staking transaction as they require special attention.
                    // A staking transaction spends one of our inputs into 2 outputs, paid to the same address.
                    if (transaction.SpendingDetails?.IsCoinStake != null && transaction.SpendingDetails.IsCoinStake.Value)
                    {
                        // We look for the 2 outputs related to our spending input.
                        List<FlatHistory> relatedOutputs = items.Where(h => h.Transaction.Id == transaction.SpendingDetails.TransactionId && h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList();
                        if (relatedOutputs.Any())
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
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight
                            };

                            transactionItems.Add(stakingItem);
                        }

                        // No need for further processing if the transaction itself is the output of a staking transaction.
                        if (transaction.IsCoinStake != null)
                        {
                            continue;
                        }
                    }

                    // Create a record for a 'receive' transaction.
                    if (!address.IsChangeAddress())
                    {
                        // Add incoming fund transaction details.
                        var receivedItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Received,
                            ToAddress = address.Address,
                            Amount = transaction.Amount,
                            Id = transaction.Id,
                            Timestamp = transaction.CreationTime,
                            ConfirmedInBlock = transaction.BlockHeight
                        };

                        transactionItems.Add(receivedItem);
                    }

                    // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                    if (transaction.SpendingDetails != null && transaction.SpendingDetails.IsCoinStake == null)
                    {
                        // Create a record for a 'send' transaction.
                        uint256 spendingTransactionId = transaction.SpendingDetails.TransactionId;
                        var sentItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Send,
                            Id = spendingTransactionId,
                            Timestamp = transaction.SpendingDetails.CreationTime,
                            ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
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

                        // Get the change address for this spending transaction.
                        FlatHistory changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                        // Find all the spending details containing the spending transaction id and aggregate the sums.
                        // This is our best shot at finding the total value of inputs for this transaction.
                        var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                        // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                        sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                        // Mined/staked coins add more coins to the total out.
                        // That makes the fee negative. If that's the case ignore the fee.
                        if (sentItem.Fee < 0)
                        {
                            sentItem.Fee = 0;
                        }

                        if (!transactionItems.Contains(sentItem, new SentTransactionItemModelComparer()))
                        {
                            transactionItems.Add(sentItem);
                        }
                    }
                }

                // Sort and filter the history items.
                List<TransactionItemModel> itemsToInclude = transactionItems.OrderByDescending(t => t.Timestamp).ToList();
                //.Where(x => string.IsNullOrEmpty(request.SearchQuery) || (x.Id.ToString() == request.SearchQuery || x.ToAddress == request.SearchQuery || x.Payments.Any(p => p.DestinationAddress == request.SearchQuery)))
                //.Skip(request.Skip ?? 0)
                //.Take(request.Take ?? transactionItems.Count)
                //.ToList();

                model.AccountsHistoryModel.Add(new AccountHistoryModel
                {
                    TransactionsHistory = itemsToInclude,
                    Name = accountHistory.Account.Name,
                    CoinType = CoinType.Stratis,
                    HdPath = accountHistory.Account.HdPath
                });
            }

            return model;
        }

        /// <summary>
        /// RPC method to return the effect an transaction has on the wallet. It doesn't show the raw Bitcoin transaction itself. Use getrawtransaction for that.
        /// Uses the first wallet and account.
        /// </summary>
        /// <param name="txid">Transaction identifier to find.</param>
        /// <returns>Effects the transaction had on the wallet.</returns>
        [ActionName("gettransaction")]
        [ActionDescription("Gets a transaction from the wallet.")]
        public GetTransactionModel GetTransaction(string txid)
        {
            uint256 trxid;
            if (!uint256.TryParse(txid, out trxid))
                throw new ArgumentException(nameof(txid));

            var accountReference = this.GetAccount();
            var account = this.walletManager.GetAccounts(accountReference.WalletName)
                                            .Where(i => i.Name.Equals(accountReference.AccountName))
                                            .Single();

            // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
            IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistoryById(trxid, accountReference.WalletName, accountReference.AccountName).ToList();

            if (!accountsHistory.Any())
            {
                return null; // This transaction is not relevant for our wallet.
            }

            var accountHistory = accountsHistory.First();
            var accountHistoryHistory = accountHistory.History.FirstOrDefault();
            var transaction = accountHistoryHistory.Transaction;
            var isChangeAddress = accountHistoryHistory.Address.IsChangeAddress();

            var confirmations = GetConformationCount(accountHistoryHistory.Transaction);

            var details = new List<GetTransactionDetailsModel>();

            // Calculate the actual effect on the wallet that this transaction had.
            Money amount = 0;

            if (isChangeAddress)
            {
                // The actual amount of sent amount is accessible on the Payments that exists on the TransactionData that is the source of this
                // input. So we need to query the history to get that other transaction, so we can parse its Payments, and not those that exists
                // on the this current transaction.
                var paymentTransactions = account.GetTransactionsByPaymentTransactionId(trxid);

                foreach (var paymentTransaction in paymentTransactions)
                {
                    // For change address history, the Amount property on the transaction will display the amount that was sent into the change address.
                    // What we need for this RPC call, is the amount that was "taken out" of the wallet, and that is available in the spending details.
                    foreach (var payment in paymentTransaction.SpendingDetails.Payments)
                    {
                        // Increase the total amount of change in the wallet if there are multiple payments.
                        amount += -payment.Amount;

                        details.Add(new GetTransactionDetailsModel
                        {
                            Account = string.Empty, // Can be empty string for default account.
                            Address = payment.DestinationAddress,
                            Category = "send",
                            Amount = -payment.Amount
                        });
                    }
                }
            }
            else
            {
                if (transaction.IsCoinStake.GetValueOrDefault(false))
                {
                    // Since this is a coin stake output, we'll hard-code to the ProofOfStakeReward. If this is implemented with a variable
                    // output, then this will likely result in invalid responses.
                    amount = this.FullNode.Network.Consensus.ProofOfStakeReward;

                    details.Add(new GetTransactionDetailsModel
                    {
                        Account = string.Empty, // Can be empty string for default account.
                        Address = accountHistoryHistory.Address.Address,
                        Category = "stake",
                        Amount = amount
                    });
                }
                else
                {
                    amount = transaction.Amount;

                    // For "receive" payments, we can read the actual Amount on the TransactionData instance. This will display the correct change on the wallet.
                    details.Add(new GetTransactionDetailsModel
                    {
                        Account = string.Empty, // Can be empty string for default account.
                        Address = accountHistoryHistory.Address.Address,
                        Category = "receive",
                        Amount = amount
                    });
                }
            }

            var model = new GetTransactionModel
            {
                Amount = amount,
                BlockHash = transaction.BlockHash,
                TransactionId = transaction.Id,
                TransactionTime = transaction.CreationTime.ToUnixTimeSeconds(),
                Confirmations = confirmations,
                Details = details,
                Hex = transaction.Hex == null ? string.Empty : transaction.Hex,
            };

            return model;
        }

        [ActionName("listunspent")]
        [ActionDescription("Returns an array of unspent transaction outputs belonging to this wallet.")]
        public UnspentCoinModel[] ListUnspent(int minConfirmations = 1, int maxConfirmations = 9999999, string addressesJson = null)
        {
            List<BitcoinAddress> addresses = new List<BitcoinAddress>();
            if (!string.IsNullOrEmpty(addressesJson))
            {
                JsonConvert.DeserializeObject<List<string>>(addressesJson).ForEach(i => addresses.Add(BitcoinAddress.Create(i, this.fullNode.Network)));
            }
            var accountReference = this.GetAccount();
            IEnumerable<UnspentOutputReference> spendableTransactions = this.walletManager.GetSpendableTransactionsInAccount(accountReference, minConfirmations);

            var unspentCoins = new List<UnspentCoinModel>();
            foreach (var spendableTx in spendableTransactions)
            {               
                if (spendableTx.Confirmations <= maxConfirmations)
                {
                    if (!addresses.Any() || addresses.Contains(BitcoinAddress.Create(spendableTx.Address.Address, this.fullNode.Network)))
                    {
                        unspentCoins.Add(new UnspentCoinModel()
                        {
                            Account = accountReference.AccountName,
                            Address = spendableTx.Address.Address,
                            Id = spendableTx.Transaction.Id,
                            Index = spendableTx.Transaction.Index,
                            Amount = spendableTx.Transaction.Amount,
                            ScriptPubKeyHex = spendableTx.Transaction.ScriptPubKey.ToHex(),
                            RedeemScriptHex = null, // TODO: Currently don't support P2SH wallet addresses, review if we do.
                            Confirmations = spendableTx.Confirmations,
                            IsSpendable = spendableTx.Transaction.IsSpendable(),
                            IsSolvable = spendableTx.Transaction.IsSpendable() // If it's spendable we assume it's solvable.
                            });
                    }
                }
            }

            return unspentCoins.ToArray();
        }

        private WalletAccountReference GetAccount()
        {
            //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            if (walletName == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");
            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            return new WalletAccountReference(walletName, account.Name);
        }
    }
}
