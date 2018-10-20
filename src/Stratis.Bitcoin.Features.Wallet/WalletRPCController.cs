using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;

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

        public WalletRPCController(IWalletManager walletManager, IFullNode fullNode, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory) : base(fullNode: fullNode)
        {
            this.walletManager = walletManager;
            this.fullNode = fullNode;
            this.broadcasterManager = broadcasterManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("sendtoaddress")]
        [ActionDescription("Sends money to a bitcoin address.")]
        public uint256 SendToAddress(BitcoinAddress bitcoinAddress, Money amount)
        {
            WalletAccountReference account = this.GetAccount();
            return uint256.Zero;
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
        /// <returns>The new address.</returns>
        [ActionName("getnewaddress")]
        [ActionDescription("Returns a new wallet address for receiving payments.")]
        public NewAddressModel GetNewAddress()
        {
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

        /// <summary>
        /// RPC method to return transaction info from the wallet.
        /// Uses the first wallet and account.
        /// </summary>
        /// <param name="txid">Transaction identifier to find.</param>
        /// <returns>Transaction information.</returns>
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

            var transaction = account.GetTransactionsById(trxid)?.Single();

            if (transaction == null)
                return null;

            var model = new GetTransactionModel
            {
                Amount = transaction.Amount,
                BlockHash = transaction.BlockHash,
                TransactionId = transaction.Id,
                TransactionTime = transaction.CreationTime.ToUnixTimeSeconds(),
                Details = new List<GetTransactionDetailsModel>(),
                Hex = transaction.Hex == null ? string.Empty : transaction.Hex
            };

            if (transaction.SpendingDetails?.Payments != null)
            {
                foreach (var paymentDetail in transaction.SpendingDetails.Payments)
                {
                    model.Details.Add(new GetTransactionDetailsModel
                    {
                        Address = paymentDetail.DestinationAddress,
                        Category = "send",
                        Amount = paymentDetail.Amount
                    });
                }
            }

            return model;
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
