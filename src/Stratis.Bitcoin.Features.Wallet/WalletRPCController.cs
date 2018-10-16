﻿using System;
using System.Linq;
using System.Security;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Controllers;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

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

        public WalletRPCController(IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IFullNode fullNode, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory) : base(fullNode: fullNode)
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

            // Length of expiry of the unlocking, restricted to max duration.
            TimeSpan duration = new TimeSpan(0, 0, Math.Min(timeout, maxDurationInSeconds));

            try
            {
                this.walletTransactionHandler.CacheSecret(account, passphrase, duration);
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
                Recipients = new [] {new Recipient { Amount = Money.Coins(amount), ScriptPubKey = address.ScriptPubKey } }.ToList(),
                WalletPassword = "_" // Want private key to pull from cache, so pass in non empty string so tx will be signed.
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
        /// RPC method that returns the spendable balance of all accounts.
        /// Uses the first wallet and account.
        /// </summary>
        /// <returns>Total spendable balance of the wallet.</returns>
        [ActionName("getbalance")]
        [ActionDescription("Gets wallets spendable balance.")]
        public decimal GetBalance()
        {
            var account = this.GetAccount();

            IEnumerable<AccountBalance> balances = this.walletManager.GetBalances(account.WalletName, account.AccountName);

            Money balance = balances?.Sum(i => i.AmountConfirmed);
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
