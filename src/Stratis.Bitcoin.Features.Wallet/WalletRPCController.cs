using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.Wallet
{
    public class WalletRPCController : FeatureController
    {
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

        private readonly WalletSettings walletSettings;

        public WalletRPCController(IWalletManager walletManager, 
            IWalletTransactionHandler walletTransactionHandler, 
            IFullNode fullNode, 
            IBroadcasterManager broadcasterManager,
            IConsensusManager consensusManager,
            ConcurrentChain chain,
            ILoggerFactory loggerFactory,
            WalletSettings walletSettings) : base(fullNode: fullNode, consensusManager: consensusManager, chain: chain)
        {
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.fullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.walletSettings = walletSettings;
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
                this.walletManager.UnlockWallet(passphrase, account.WalletName, timeout);
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
            this.walletManager.LockWallet(account.WalletName);
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
                Recipients = new [] {new Recipient { Amount = Money.Coins(amount), ScriptPubKey = address.ScriptPubKey } }.ToList(),
                CacheSecret = false
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
        public decimal GetBalance(string accountName, int minConfirmations = 0)
        {
            if (!string.IsNullOrEmpty(accountName) && !accountName.Equals("*"))
                throw new RPCServerException(RPCErrorCode.RPC_METHOD_DEPRECATED, "Account has been deprecated, must be excluded or set to \"*\"");

            WalletAccountReference account = this.GetAccount();

            Money balance = this.walletManager.GetSpendableTransactionsInAccount(account, minConfirmations).Sum(x => x.Transaction.Amount);
            return balance?.ToUnit(MoneyUnit.BTC) ?? 0;
        }

        /// <summary>
        /// RPC method to return transaction info from the wallet. Will only work fully if 'txindex' is set.
        /// Uses the default wallet if specified, or the first wallet found.
        /// </summary>
        /// <param name="txid">Identifier of the transaction to find.</param>
        /// <returns>Transaction information.</returns>
        [ActionName("gettransaction")]
        [ActionDescription("Get detailed information about an in-wallet transaction.")]
        public async Task<GetTransactionModel> GetTransactionAsync(string txid)
        {
            if (!uint256.TryParse(txid, out uint256 trxid))
                throw new ArgumentException(nameof(txid));

            WalletAccountReference accountReference = this.GetAccount();
            HdAccount account = this.walletManager.GetAccounts(accountReference.WalletName).Single(a => a.Name == accountReference.AccountName);

            // Get the transaction from the wallet by looking into received and send transactions.
            List<HdAddress> addresses = account.GetCombinedAddresses().ToList();
            List<TransactionData> receivedTransactions = addresses.Where(r => !r.IsChangeAddress() && r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.Id == trxid)).ToList();
            List<TransactionData> sendTransactions = addresses.Where(r => r.Transactions != null).SelectMany(a => a.Transactions.Where(t => t.SpendingDetails != null && t.SpendingDetails.TransactionId == trxid)).ToList();

            if (!receivedTransactions.Any() && !sendTransactions.Any())
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_ADDRESS_OR_KEY, "Invalid or non-wallet transaction id.");

            // Get the block hash from the transaction in the wallet.
            TransactionData transactionFromWallet = null;
            uint256 blockHash = null;
            int? blockHeight, blockIndex;

            if (receivedTransactions.Any())
            {
                blockHeight = receivedTransactions.First().BlockHeight;
                blockIndex = receivedTransactions.First().BlockIndex;
                blockHash = receivedTransactions.First().BlockHash;
                transactionFromWallet = receivedTransactions.First();
            }
            else
            {
                blockHeight = sendTransactions.First().SpendingDetails.BlockHeight;
                blockIndex = sendTransactions.First().SpendingDetails.BlockIndex;
                blockHash = blockHeight != null ? this.Chain.GetBlock(blockHeight.Value).HashBlock : null;
            }

            // Get the block containing the transaction (if it has  been confirmed).
            ChainedHeaderBlock chainedHeaderBlock = null;
            if (blockHash != null)
            {
                await this.ConsensusManager.GetOrDownloadBlocksAsync(new List<uint256> {blockHash}, b => { chainedHeaderBlock = b; });
            }

            Block block = null;
            Transaction transactionFromStore = null;
            if (chainedHeaderBlock != null)
            {
                block = chainedHeaderBlock.Block;
                transactionFromStore = block.Transactions.Single(t => t.GetHash() == trxid);
            }

            DateTimeOffset transactionTime;
            bool isGenerated;
            string hex;
            if (transactionFromStore != null)
            {
                transactionTime = Utils.UnixTimeToDateTime(transactionFromStore.Time);
                isGenerated = transactionFromStore.IsCoinBase || transactionFromStore.IsCoinStake;
                hex = transactionFromStore.ToHex();

            }
            else if(transactionFromWallet != null)
            {
                transactionTime = transactionFromWallet.CreationTime;
                isGenerated = transactionFromWallet.IsCoinBase == true || transactionFromWallet.IsCoinStake == true;
                hex = transactionFromWallet.Hex;
            }
            else
            {
                transactionTime = sendTransactions.First().SpendingDetails.CreationTime;
                isGenerated = false;
                hex = null; // TODO get from mempool
            }

            Money amountSent = sendTransactions.Select(s => s.SpendingDetails).SelectMany(sds => sds.Payments).GroupBy(p => p.DestinationAddress).Select(g => g.First()).Sum(p => p.Amount);
            Money totalAmount = receivedTransactions.Sum(t => t.Amount) - amountSent;

            var model = new GetTransactionModel
            {
                Amount = totalAmount.ToDecimal(MoneyUnit.BTC),
                Fee = null,// TODO this still needs to be worked on.
                Confirmations = blockHeight != null ? this.ConsensusManager.Tip.Height - blockHeight.Value + 1 : 0,
                Isgenerated = isGenerated ? true : (bool?) null,
                BlockHash = blockHash,
                BlockIndex = blockIndex ?? block?.Transactions.FindIndex(t => t.GetHash() == trxid),
                BlockTime = block?.Header.BlockTime.ToUnixTimeSeconds(),
                TransactionId = uint256.Parse(txid),
                TransactionTime = transactionTime.ToUnixTimeSeconds(),
                TimeReceived = transactionTime.ToUnixTimeSeconds(),
                Details = new List<GetTransactionDetailsModel>(),
                Hex = hex
            };

            // Send transactions details.
            foreach (PaymentDetails paymentDetail in sendTransactions.Select(s => s.SpendingDetails).SelectMany(sd => sd.Payments))
            {
                // Only a single item should appear per destination address.
                if (model.Details.SingleOrDefault(d => d.Address == paymentDetail.DestinationAddress) == null)
                {
                    model.Details.Add(new GetTransactionDetailsModel
                    {
                        Address = paymentDetail.DestinationAddress,
                        Category = GetTransactionDetailsCategoryModel.Send,
                        Amount = -paymentDetail.Amount.ToDecimal(MoneyUnit.BTC),
                        Fee = null, // TODO this still needs to be worked on.
                        OutputIndex = paymentDetail.OutputIndex
                    });
                }
            }

            // Receive transactions details.
            foreach (TransactionData trxInWallet in receivedTransactions)
            {
                GetTransactionDetailsCategoryModel category;
                if (isGenerated)
                {
                    category = model.Confirmations > this.FullNode.Network.Consensus.CoinbaseMaturity ? GetTransactionDetailsCategoryModel.Generate : GetTransactionDetailsCategoryModel.Immature;
                }
                else
                {
                    category = GetTransactionDetailsCategoryModel.Receive;
                }

                model.Details.Add(new GetTransactionDetailsModel
                {
                    Address = addresses.First(a => a.Transactions.Contains(trxInWallet)).Address,
                    Category = category,
                    Amount = trxInWallet.Amount.ToDecimal(MoneyUnit.BTC),
                    OutputIndex = trxInWallet.Index
                });
            }

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

            WalletAccountReference accountReference = this.GetAccount();
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
                            IsSpendable = !spendableTx.Transaction.IsSpent(),
                            IsSolvable = !spendableTx.Transaction.IsSpent() // If it's spendable we assume it's solvable.
                        });
                    }
                }
            }

            return unspentCoins.ToArray();
        }

        [ActionName("sendmany")]
        [ActionDescription("Creates and broadcasts a transaction which sends outputs to multiple addresses.")]
        public async Task<uint256> SendManyAsync(string fromAccount, string addressesJson, int minConf = 1, string comment = null, string subtractFeeFromJson = null, bool isReplaceable = false, int? confTarget = null, string estimateMode = "UNSET")
        {
            if (string.IsNullOrEmpty(addressesJson))
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_PARAMETER, "No valid output addresses specified.");

            var addresses = new Dictionary<string, decimal>();
            try
            {
                // Outputs addresses are keyvalue pairs of address, amount. Translate to Receipient list.
                addresses = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(addressesJson);
            }
            catch (JsonSerializationException ex)
            {
                throw new RPCServerException(RPCErrorCode.RPC_PARSE_ERROR, ex.Message);
            }

            if (addresses.Count == 0)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_PARAMETER, "No valid output addresses specified.");

            // Optional list of addresses to subtract fees from.
            IEnumerable<BitcoinAddress> subtractFeeFromAddresses = null;
            if (!string.IsNullOrEmpty(subtractFeeFromJson))
            {
                try
                {
                    subtractFeeFromAddresses = JsonConvert.DeserializeObject<List<string>>(subtractFeeFromJson).Select(i => BitcoinAddress.Create(i, this.fullNode.Network));
                }
                catch (JsonSerializationException ex)
                {
                    throw new RPCServerException(RPCErrorCode.RPC_PARSE_ERROR, ex.Message);
                }
            }

            var recipients = new List<Recipient>();
            foreach (var address in addresses)
            {
                // Check for duplicate recipients
                var recipientAddress = BitcoinAddress.Create(address.Key, this.fullNode.Network).ScriptPubKey;
                if (recipients.Any(r => r.ScriptPubKey == recipientAddress))
                    throw new RPCServerException(RPCErrorCode.RPC_INVALID_PARAMETER, string.Format("Invalid parameter, duplicated address: {0}.", recipientAddress));

                var recipient = new Recipient
                {
                    ScriptPubKey = recipientAddress,
                    Amount = Money.Coins(address.Value),
                    SubtractFeeFromAmount = subtractFeeFromAddresses == null ? false : subtractFeeFromAddresses.Contains(BitcoinAddress.Create(address.Key, this.fullNode.Network))
                };

                recipients.Add(recipient);
            }

            WalletAccountReference accountReference = this.GetAccount();

            var context = new TransactionBuildContext(this.fullNode.Network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConf,
                Shuffle = true, // We shuffle transaction outputs by default as it's better for anonymity.
                Recipients = recipients,
                CacheSecret = false
            };

            // Set fee type for transaction build context.
            context.FeeType = FeeType.Medium;

            if (estimateMode.Equals("ECONOMICAL", StringComparison.InvariantCultureIgnoreCase))
                context.FeeType = FeeType.Low;

            else if (estimateMode.Equals("CONSERVATIVE", StringComparison.InvariantCultureIgnoreCase))
                context.FeeType = FeeType.High;

            try
            {
                // Log warnings for currently unsupported parameters.
                if (!string.IsNullOrEmpty(comment))
                    this.logger.LogWarning("'comment' parameter is currently unsupported. Ignored.");

                if (isReplaceable)
                    this.logger.LogWarning("'replaceable' parameter is currently unsupported. Ignored.");

                if (confTarget != null)
                    this.logger.LogWarning("'conf_target' parameter is currently unsupported. Ignored.");

                Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);
                await this.broadcasterManager.BroadcastTransactionAsync(transaction);

                return transaction.GetHash();
            }
            catch (SecurityException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_WALLET_UNLOCK_NEEDED, exception.Message);
            }
            catch (WalletException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_WALLET_ERROR, exception.Message);
            }
            catch (NotImplementedException exception)
            {
                throw new RPCServerException(RPCErrorCode.RPC_MISC_ERROR, exception.Message);
            }
        }

        /// <summary>
        /// Gets the first account from the "default" wallet if it specified, otherwise returns the first available account in the existing wallets.
        /// </summary>
        /// <returns>Reference to the default wallet account, or the first available if no default wallet is specified.</returns>
        private WalletAccountReference GetAccount()
        {
            string walletName;

            if (this.walletSettings.IsDefaultWalletEnabled())
            {
                walletName = this.walletManager.GetWalletsNames().FirstOrDefault(w => w == this.walletSettings.DefaultWalletName);
            }
            else
            {
                //TODO: Support multi wallet like core by mapping passed RPC credentials to a wallet/account
                walletName = this.walletManager.GetWalletsNames().FirstOrDefault();
            }

            if (walletName == null)
                throw new RPCServerException(RPCErrorCode.RPC_INVALID_REQUEST, "No wallet found");

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();
            return new WalletAccountReference(walletName, account.Name);
        }
    }
}
