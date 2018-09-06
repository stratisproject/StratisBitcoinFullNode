using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

[assembly: InternalsVisibleTo("Stratis.Bitcoin.Features.ColdStaking.Tests")]

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// The manager class for implementing cold staking as discussed in <see cref="ColdStakingFeature"/>.
    /// The class provides the methods used by the <see cref="Controllers.ColdStakingController"/>.
    /// </summary>
    /// <remarks>
    /// See the comments for each method listed below for more details.
    /// </remarks>
    /// <seealso cref="ColdStakingFeature"/>
    /// <seealso cref="GetColdStakingAddress(Wallet.Wallet, bool, string)"/>
    /// <seealso cref="GetColdStakingAccount(Wallet.Wallet, bool, string)"/>
    /// <seealso cref="GetColdStakingScript(ScriptId, ScriptId)"/>
    /// <seealso cref="GetSetupBuildContext(string, string, string, string, string, Money, Money)"/>
    public class ColdStakingManager
    {
        /// <summary>The account index of the cold wallet account.</summary>
        private const int ColdWalletAccountIndex = Wallet.Wallet.ColdStakingAccountIndex + 0;

        /// <summary>The account index of the hot wallet account.</summary>
        private const int HotWalletAccountIndex = Wallet.Wallet.ColdStakingAccountIndex + 1;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The wallet manager to use for accessing wallets and their accounts.</summary>
        public IWalletManager WalletManager { get; private set; }

        /// <summary>The wallet transaction handler to use for building transactions.</summary>
        public IWalletTransactionHandler WalletTransactionHandler { get; private set; }

        /// <summary>Provider of time functions.</summary>
        public IDateTimeProvider DateTimeProvider { get; private set; }

        /// <summary>
        /// Constructs the cold staking manager which is used by the cold staking controller.
        /// </summary>
        /// <param name="loggerFactory">The logger factory to use to create the custom logger.</param>
        /// <param name="walletManager">The wallet manager to use for accessing wallets and their accounts.</param>
        /// <param name="walletTransactionHandler">The wallet transaction handler to use for building transactions.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        public ColdStakingManager(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IDateTimeProvider dateTimeProvider)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(walletManager, nameof(walletManager));
            Guard.NotNull(walletTransactionHandler, nameof(walletTransactionHandler));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.WalletManager = walletManager;
            this.WalletTransactionHandler = walletTransactionHandler;
            this.DateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Gets a cold staking account. Creates the account if it does not exist.
        /// </summary>
        /// <remarks>
        /// <para>In order to keep track of cold staking addresses and balances we are using <see cref="HdAccount"/>'s
        /// with indexes starting from the value defined in <see cref="Wallet.Wallet.ColdStakingAccountIndex"/>.
        /// </para><para>
        /// We are using two such accounts, one when the wallet is in the role of cold wallet, and another one when
        /// the wallet is in the role of hot wallet. For this reason we specify the required account when calling this
        /// method.
        /// </para></remarks>
        /// <param name="wallet">The wallet where we wish to create the account.</param>
        /// <param name="isColdWalletAccount">Indicates whether we need the cold wallet account (versus the hot wallet account).</param>
        /// <param name="walletPassword">The (optional) wallet password. If not <c>null</c> the account will be created if it does not exist.</param>
        /// <returns>The cold staking account or null if the account does not exist.</returns>
        internal HdAccount GetColdStakingAccount(Wallet.Wallet wallet, bool isColdWalletAccount, string walletPassword = null)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3},{4}:{5})",
                nameof(wallet), wallet.Name,
                nameof(isColdWalletAccount), isColdWalletAccount
                );

            bool createIfNotExists = !string.IsNullOrEmpty(walletPassword);

            int accountIndex = isColdWalletAccount ? ColdWalletAccountIndex : HotWalletAccountIndex;

            var coinType = (CoinType)wallet.Network.Consensus.CoinType;

            HdAccount account = wallet.GetAccountsByCoinType(coinType).FirstOrDefault(a => a.Index == accountIndex);

            if (account == null)
            {
                if (!createIfNotExists)
                {
                    this.logger.LogTrace("(-)[ACCOUNT_DOES_NOT_EXIST]");
                    return null;
                }

                AccountRoot accountRoot = wallet.AccountsRoot.Single(a => a.CoinType == coinType);

                account = accountRoot.CreateAccount(walletPassword, wallet.EncryptedSeed,
                    wallet.ChainCode, wallet.Network, this.DateTimeProvider.GetTimeOffset(), accountIndex);

                ICollection<HdAccount> hdAccounts = accountRoot.Accounts.ToList();
                hdAccounts.Add(account);
                accountRoot.Accounts = hdAccounts;
            }

            if (account.ExternalAddresses.Count == 0)
                account.CreateAddresses(wallet.Network, 1, false);

            this.logger.LogTrace("(-):'{0}'", account?.Name);
            return account;
        }

        /// <summary>
        /// Gets a cold staking address.
        /// </summary>
        /// <param name="wallet">The wallet providing the cold staking address.</param>
        /// <param name="isColdWalletAddress">Indicates whether we need the cold wallet address (versus the hot wallet address).</param>
        /// <param name="walletPassword">The (optional) wallet password.  If not <c>null</c> the account will be created if it does not exist.</param>
        /// <returns>The cold staking address or null if the required account does not exist.</returns>
        internal HdAddress GetColdStakingAddress(Wallet.Wallet wallet, bool isColdWalletAddress, string walletPassword = null)
        {
            Guard.NotNull(wallet, nameof(wallet));

            this.logger.LogTrace("({0}:'{1}',{2}:{3})",
                nameof(wallet), wallet.Name,
                nameof(isColdWalletAddress), isColdWalletAddress
                );

            HdAddress address = this.GetColdStakingAccount(wallet, isColdWalletAddress, walletPassword)?.ExternalAddresses.First();

            this.logger.LogTrace("(-):'{0}'", address?.Address);
            return address;
        }

        /// <summary>
        /// Creates a cold staking script.
        /// </summary>
        /// <remarks>Two keys control the balance associated with the script.
        /// The hot wallet key allows transactions to only spend amounts back to themselves while the cold
        /// wallet key allows amounts to be moved to different addresses. This makes it possible to perform
        /// staking using the hot wallet key so that even if the key becomes compromised it can't be used
        /// to reduce the balance. Only the person with the cold wallet key can retrieve the coins and move
        /// them elsewhere. This behavior is enforced by the <see cref="OpcodeType.OP_CHECKCOLDSTAKEVERIFY"/>
        /// opcode which sets the <see cref="PosTransaction.IsColdCoinStake"/> flag if the transaction spending
        /// an output, which contains this instruction, is a coinstake transaction. If this flag is set then
        /// further rules are enforced by <see cref="Consensus.Rules.CommonRules.PosColdStakingRule"/>.
        /// </remarks>
        /// <param name="hotPubKey">The "hotPubKey" to use.</param>
        /// <param name="coldPubKey">The "coldPubKey" to use.</param>
        /// <returns>The cold staking script.</returns>
        /// <seealso cref="Consensus.Rules.CommonRules.PosColdStakingRule"/>
        private Script GetColdStakingScript(ScriptId hotPubKey, ScriptId coldPubKey)
        {
            return new Script(OpcodeType.OP_DUP, OpcodeType.OP_HASH160, OpcodeType.OP_ROT,
                OpcodeType.OP_IF, OpcodeType.OP_CHECKCOLDSTAKEVERIFY,
                Op.GetPushOp(hotPubKey.ToBytes()),
                OpcodeType.OP_ELSE,
                Op.GetPushOp(coldPubKey.ToBytes()),
                OpcodeType.OP_ENDIF,
                OpcodeType.OP_EQUALVERIFY, OpcodeType.OP_CHECKSIG);
        }

        /// <summary>
        /// Creates a <see cref="TransactionBuildContext"/> for creating a cold staking setup transaction.
        /// </summary>
        /// <param name="coldWalletAddress">The cold wallet address generated by <see cref="GetColdStakingAddress(IWalletManager, CoinType, string, string, bool)"/></param>
        /// <param name="hotWalletAddress">The hot wallet address generated by <see cref="GetColdStakingAddress(IWalletManager, CoinType, string, string, bool)"/></param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletAccount">The wallet account.</param>
        /// <param name="walletPassword">The wallet password.</param>
        /// <param name="amount">The amount to cold stake.</param>
        /// <param name="feeAmount">The fee to pay for the cold staking setup transaction.</param>
        /// <returns>The <see cref="TransactionBuildContext"/> for creating the cold staking setup transaction.</returns>
        /// <exception cref="WalletException">Thrown if the same wallet is being used as both the hot wallet and cold wallet.</exception>
        /// <exception cref="WalletException">Thrown if the hot and cold wallet addresses could not be found in the corresponding accounts.</exception>
        /// <exception cref="WalletException">Thrown if an attempt is made to spend coins from a cold staking account.</exception>
        internal TransactionBuildContext GetSetupBuildContext(
            string coldWalletAddress, string hotWalletAddress, string walletName, string walletAccount,
            string walletPassword, Money amount, Money feeAmount)
        {
            Guard.NotEmpty(coldWalletAddress, nameof(coldWalletAddress));
            Guard.NotEmpty(hotWalletAddress, nameof(hotWalletAddress));
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotEmpty(walletAccount, nameof(walletAccount));
            Guard.NotNull(amount, nameof(amount));
            Guard.NotNull(feeAmount, nameof(feeAmount));

            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}',{6}:'{7}',{8}:{9},{10}:{11})",
                nameof(coldWalletAddress), coldWalletAddress,
                nameof(hotWalletAddress), hotWalletAddress,
                nameof(walletName), walletName,
                nameof(walletAccount), walletAccount,
                nameof(amount), amount,
                nameof(feeAmount), feeAmount
                );

            Wallet.Wallet wallet = this.WalletManager.GetWalletByName(walletName);
            HdAccount coldAccount = this.GetColdStakingAccount(wallet, true, walletPassword);
            HdAccount hotAccount = this.GetColdStakingAccount(wallet, false, walletPassword);

            bool thisIsColdWallet = coldAccount?.ExternalAddresses.Select(a => a.Address).Contains(coldWalletAddress) ?? false;
            bool thisIsHotWallet = hotAccount?.ExternalAddresses.Select(a => a.Address).Contains(hotWalletAddress) ?? false;

            if (thisIsColdWallet && thisIsHotWallet)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_BOTH_HOT_AND_COLD]");
                throw new WalletException("You can't use this wallet as both hot wallet and cold wallet.");
            }

            if (!thisIsColdWallet && !thisIsHotWallet)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_ADDRESSES_NOT_IN_ACCOUNTS]");
                throw new WalletException("The hot and cold wallet addresses could not be found in the corresponding accounts.");
            }

            ScriptId hotPubKey = BitcoinAddress.Create(hotWalletAddress, wallet.Network).ScriptPubKey.Hash;
            ScriptId coldPubKey = BitcoinAddress.Create(coldWalletAddress, wallet.Network).ScriptPubKey.Hash;
            Script destination = this.GetColdStakingScript(hotPubKey, coldPubKey);

            // Only normal accounts should be allowed.
            if (this.WalletManager.GetAccounts(walletName).Single(a => a.Name == walletAccount).Index >= ColdWalletAccountIndex)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_OPERATION_NOT_ALLOWED]");
                throw new WalletException($"You can't perform this operation with wallet account '{ walletAccount }'.");
            }

            var context = new TransactionBuildContext(wallet.Network)
            {
                AccountReference = new WalletAccountReference(walletName, walletAccount),
                TransactionFee = feeAmount,
                MinConfirmations = 0,
                Shuffle = true,
                OpReturnData = (thisIsHotWallet ? hotPubKey : coldPubKey).ToString(),
                WalletPassword = walletPassword,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList()
            };

            this.logger.LogTrace("(-)");
            return context;
        }
    }
}
