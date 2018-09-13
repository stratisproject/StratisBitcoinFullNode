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
    /// The manager class for implementing cold staking as covered in more detail in the remarks of
    /// the <see cref="ColdStakingFeature"/> class.
    /// This class provides the methods used by the <see cref="Controllers.ColdStakingController"/>
    /// which in turn provides the API methods for accessing this functionality.
    /// </summary>
    /// <remarks>
    /// The following functionality is implemented in this class:
    /// <list type="bullet">
    /// <item><description>Generating cold staking address via the <see cref="GetFirstUnusedColdStakingAddress"/> method. These
    /// addresses are used for generating the cold staking setup.</description></item>
    /// <item><description>Creating a build context for generating the cold staking setup via the <see
    /// cref="GetColdStakingSetupTransaction"/> method.</description></item>
    /// </list>
    /// </remarks>
    public class ColdStakingManager
    {
        /// <summary>The account index of the cold wallet account.</summary>
        private const int ColdWalletAccountIndex = Wallet.Wallet.SpecialPurposeAccountIndexesStart + 0;

        /// <summary>The account name of the cold wallet account.</summary>
        private const string ColdWalletAccountName = "coldStakingColdAddresses";

        /// <summary>The account index of the hot wallet account.</summary>
        private const int HotWalletAccountIndex = Wallet.Wallet.SpecialPurposeAccountIndexesStart + 1;

        /// <summary>The account name of the hot wallet account.</summary>
        private const string HotWalletAccountName = "coldStakingHotAddresses";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The wallet manager to use for accessing wallets and their accounts.</summary>
        private readonly IWalletManager walletManager;

        /// <summary>The wallet transaction handler to use for building transactions.</summary>
        private readonly IWalletTransactionHandler walletTransactionHandler;

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
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Returns information related to cold staking.
        /// </summary>
        /// <param name="walletName">The wallet to return the information for.</param>
        /// <returns>A <see cref="Models.GetColdStakingInfoResponse"/> object containing the information.</returns>
        internal Models.GetColdStakingInfoResponse GetColdStakingInfo(string walletName)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(walletName), walletName);

            Wallet.Wallet wallet = this.walletManager.GetWalletByName(walletName);
            var coinType = (CoinType)wallet.Network.Consensus.CoinType;
            List<HdAccount> accounts = wallet.GetAccountsByCoinType(coinType).ToList();

            var response = new Models.GetColdStakingInfoResponse()
            {
                ColdWalletAccountExists = accounts.Any(a => a.Index == ColdWalletAccountIndex),
                HotWalletAccountExists = accounts.Any(a => a.Index == HotWalletAccountIndex)
            };

            this.logger.LogTrace("(-):'{0}'", response);
            return response;
        }

        /// <summary>
        /// Gets a cold staking account.
        /// </summary>
        /// <remarks>
        /// <para>In order to keep track of cold staking addresses and balances we are using <see cref="HdAccount"/>'s
        /// with indexes starting from the value defined in <see cref="Wallet.Wallet.SpecialPurposeAccountIndexesStart"/>.
        /// </para><para>
        /// We are using two such accounts, one when the wallet is in the role of cold wallet, and another one when
        /// the wallet is in the role of hot wallet. For this reason we specify the required account when calling this
        /// method.
        /// </para></remarks>
        /// <param name="wallet">The wallet where we wish to create the account.</param>
        /// <param name="isColdWalletAccount">Indicates whether we need the cold wallet account (versus the hot wallet account).</param>
        /// <returns>The cold staking account or <c>null</c> if the account does not exist.</returns>
        internal HdAccount GetColdStakingAccount(Wallet.Wallet wallet, bool isColdWalletAccount)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(wallet), wallet.Name, nameof(isColdWalletAccount), isColdWalletAccount);

            int accountIndex = isColdWalletAccount ? ColdWalletAccountIndex : HotWalletAccountIndex;
            var coinType = (CoinType)wallet.Network.Consensus.CoinType;
            HdAccount account = wallet.GetAccountsByCoinType(coinType).FirstOrDefault(a => a.Index == accountIndex);

            if (account == null)
            {
                this.logger.LogTrace("(-)[ACCOUNT_DOES_NOT_EXIST]:null");
                return null;
            }

            this.logger.LogTrace("(-):'{0}'", account.Name);
            return account;
        }

        /// <summary>
        /// Creates a cold staking account and ensures that it has at least one address.
        /// If the account already exists then the existing account is returned.
        /// </summary>
        /// <remarks>
        /// <para>In order to keep track of cold staking addresses and balances we are using <see cref="HdAccount"/>'s
        /// with indexes starting from the value defined in <see cref="Wallet.Wallet.SpecialPurposeAccountIndexesStart"/>.
        /// </para><para>
        /// We are using two such accounts, one when the wallet is in the role of cold wallet, and another one when
        /// the wallet is in the role of hot wallet. For this reason we specify the required account when calling this
        /// method.
        /// </para></remarks>
        /// <param name="walletName">The name of the wallet where we wish to create the account.</param>
        /// <param name="isColdWalletAccount">Indicates whether we need the cold wallet account (versus the hot wallet account).</param>
        /// <param name="walletPassword">The wallet password which will be used to create the account.</param>
        /// <returns>The new or existing cold staking account.</returns>
        internal HdAccount GetOrCreateColdStakingAccount(string walletName, bool isColdWalletAccount, string walletPassword)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletName), walletName, nameof(isColdWalletAccount), isColdWalletAccount);

            Wallet.Wallet wallet = this.walletManager.GetWalletByName(walletName);

            HdAccount account = this.GetColdStakingAccount(wallet, isColdWalletAccount);
            if (account != null)
            {
                this.logger.LogTrace("(-)[ACCOUNT_ALREADY_EXIST]:'{0}'", account.Name);
                return account;
            }

            int accountIndex = isColdWalletAccount ? ColdWalletAccountIndex : HotWalletAccountIndex;
            var coinType = (CoinType)wallet.Network.Consensus.CoinType;

            this.logger.LogTrace("The {0} wallet account for '{1}' does not exist and will now be created.", isColdWalletAccount ? "cold" : "hot", wallet.Name);

            AccountRoot accountRoot = wallet.AccountsRoot.Single(a => a.CoinType == coinType);

            account = accountRoot.CreateAccount(walletPassword, wallet.EncryptedSeed,
                wallet.ChainCode, wallet.Network, this.dateTimeProvider.GetTimeOffset(), accountIndex,
                isColdWalletAccount ? ColdWalletAccountName : HotWalletAccountName);

            // Maintain at least one unused address at all times. This will ensure that wallet recovery will also work.
            account.CreateAddresses(wallet.Network, 1, false);

            ICollection<HdAccount> hdAccounts = accountRoot.Accounts.ToList();
            hdAccounts.Add(account);
            accountRoot.Accounts = hdAccounts;

            this.logger.LogTrace("(-):'{0}'", account.Name);
            return account;
        }

        /// <summary>
        /// Gets the first unused cold staking address. Creates a new address if required.
        /// </summary>
        /// <param name="walletName">The name of the wallet providing the cold staking address.</param>
        /// <param name="isColdWalletAddress">Indicates whether we need the cold wallet address (versus the hot wallet address).</param>
        /// <returns>The cold staking address or <c>null</c> if the required account does not exist.</returns>
        internal HdAddress GetFirstUnusedColdStakingAddress(string walletName, bool isColdWalletAddress)
        {
            Guard.NotNull(walletName, nameof(walletName));

            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(walletName), walletName, nameof(isColdWalletAddress), isColdWalletAddress);

            Wallet.Wallet wallet = this.walletManager.GetWalletByName(walletName);
            HdAccount account = this.GetColdStakingAccount(wallet, isColdWalletAddress);
            if (account == null)
            {
                this.logger.LogTrace("(-)[ACCOUNT_DOES_NOT_EXIST]:null");
                return null;
            }

            HdAddress address = account.GetFirstUnusedReceivingAddress();
            if (address == null)
            {
                this.logger.LogTrace("No unused address exists on account '{0}'. Adding new address.", account.Name);
                address = account.CreateAddresses(wallet.Network, 1).First();
            }

            this.logger.LogTrace("(-):'{0}'", address.Address);
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
        /// opcode within the script flow related to hot wallet key usage. It sets the <see cref="PosTransaction.IsColdCoinStake"/>
        /// flag if the transaction spending an output, which contains this instruction, is a coinstake
        /// transaction. If this flag is set then further rules are enforced by <see cref="Consensus.Rules.CommonRules.PosColdStakingRule"/>.
        /// </remarks>
        /// <param name="hotPubKey">The hot wallet public key to use.</param>
        /// <param name="coldPubKey">The cold wallet public key to use.</param>
        /// <returns>The cold staking script.</returns>
        /// <seealso cref="Consensus.Rules.CommonRules.PosColdStakingRule"/>
        private Script GetColdStakingScript(TxDestination hotPubKey, TxDestination coldPubKey)
        {
            // The initial stack consumed by this script will be set up differently depending on whether a
            // hot or cold pubkey is used - i.e. either <scriptSig> 0 <coldPubKey> OR <scriptSig> 1 <hotPubKey>.
            return new Script(
                // Duplicates the last stack entry resulting in:
                // <scriptSig> 0/1 <coldPubKey/hotPubKey> <coldPubKey/hotPubKey>.
                OpcodeType.OP_DUP,
                // Replaces the last stack entry with its hash resulting in:
                // <scriptSig> 0/1 <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash>.
                OpcodeType.OP_HASH160,
                // Rotates the top 3 stack entries resulting in:
                // <scriptSig> <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash> 0/1.
                OpcodeType.OP_ROT,
                // Consumes the top stack entry and continues from the OP_ELSE if the value was 0. Results in:
                // <scriptSig> <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash>.
                OpcodeType.OP_IF,
                // Reaching this point means that the value was 1 - i.e. the hotPubKey is being used.
                // Executes the opcode as described in the remarks section. Stack remains unchanged.
                OpcodeType.OP_CHECKCOLDSTAKEVERIFY,
                // Pushes the expected hotPubKey value onto the stack for later comparison purposes. Results in:
                // <scriptSig> <hotPubKey> <hotPubKeyHash> <hotPubKeyHash for comparison>.
                Op.GetPushOp(hotPubKey.ToBytes()),
                // The code contained in the OP_ELSE is executed when the value was 0 - i.e. the coldPubKey is used.
                OpcodeType.OP_ELSE,
                // Pushes the expected coldPubKey value onto the stack for later comparison purposes. Results in:
                // <scriptSig> <coldPubKey> <coldPubKeyHash> <coldPubKeyHash for comparison>.
                Op.GetPushOp(coldPubKey.ToBytes()),
                OpcodeType.OP_ENDIF,
                // Checks that the <coldPubKeyHash/hotPubKeyHash> matches the comparison value and removes both values
                // from the stack. The script fails at this point if the values mismatch. Results in:
                // <scriptSig> <coldPubKey/hotPubKey>.
                OpcodeType.OP_EQUALVERIFY,
                // Consumes the top 2 stack entries and uses those values to verify the signature. Results in:
                // true/false - i.e. true if the signature is valid and false otherwise.
                OpcodeType.OP_CHECKSIG);
        }

        /// <summary>
        /// Creates cold staking setup <see cref="Transaction"/>.
        /// </summary>
        /// <remarks>
        /// The <paramref name="coldWalletAddress"/> and <paramref name="hotWalletAddress"/> would be expected to be
        /// from different wallets and typically also different physical machines under normal circumstances. The following
        /// rules are enforced by this method and would lead to a <see cref="WalletException"/> otherwise:
        /// <list type="bullet">
        /// <item><description>The cold and hot wallet addresses are expected to belong to different wallets.</description></item>
        /// <item><description>Either the cold or hot wallet address must belong to a cold staking account in the wallet identified
        /// by <paramref name="walletName"/></description></item>
        /// <item><description>The account specified in <paramref name="walletAccount"/> can't be a cold staking account.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="coldWalletAddress">The cold wallet address generated by <see cref="GetFirstUnusedColdStakingAddress"/>.</param>
        /// <param name="hotWalletAddress">The hot wallet address generated by <see cref="GetFirstUnusedColdStakingAddress"/>.</param>
        /// <param name="walletName">The name of the wallet.</param>
        /// <param name="walletAccount">The wallet account.</param>
        /// <param name="walletPassword">The wallet password.</param>
        /// <param name="amount">The amount to cold stake.</param>
        /// <param name="feeAmount">The fee to pay for the cold staking setup transaction.</param>
        /// <returns>The <see cref="Transaction"/> for setting up cold staking.</returns>
        /// <exception cref="WalletException">Thrown if any of the rules listed in the remarks section of this method are broken.</exception>
        internal Transaction GetColdStakingSetupTransaction(
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

            Wallet.Wallet wallet = this.walletManager.GetWalletByName(walletName);

            // Get/create the cold staking accounts.
            HdAccount coldAccount = this.GetOrCreateColdStakingAccount(walletName, true, walletPassword);
            HdAccount hotAccount = this.GetOrCreateColdStakingAccount(walletName, false, walletPassword);

            bool thisIsColdWallet = coldAccount?.ExternalAddresses.Select(a => a.Address).Contains(coldWalletAddress) ?? false;
            bool thisIsHotWallet = hotAccount?.ExternalAddresses.Select(a => a.Address).Contains(hotWalletAddress) ?? false;

            this.logger.LogTrace("Local wallet '{0}' does{1} contain cold wallet address '{2}' and does{3} contain hot wallet address '{4}'.",
                walletName, thisIsColdWallet ? "" : " NOT", coldWalletAddress, thisIsHotWallet ? "" : " NOT", hotWalletAddress);

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

            TxDestination hotPubKey = BitcoinAddress.Create(hotWalletAddress, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);
            TxDestination coldPubKey = BitcoinAddress.Create(coldWalletAddress, wallet.Network).ScriptPubKey.GetDestination(wallet.Network);
            Script destination = this.GetColdStakingScript(hotPubKey, coldPubKey);

            // Only normal accounts should be allowed.
            if (!this.walletManager.GetAccounts(walletName).Any(a => a.Name == walletAccount))
            {
                this.logger.LogTrace("(-)[COLDSTAKE_ACCOUNT_NOT_FOUND]");
                throw new WalletException($"Can't find wallet account '{walletAccount}'.");
            }

            var context = new TransactionBuildContext(wallet.Network)
            {
                AccountReference = new WalletAccountReference(walletName, walletAccount),
                TransactionFee = feeAmount,
                MinConfirmations = 0,
                Shuffle = false,
                WalletPassword = walletPassword,
                Recipients = new List<Recipient>() { new Recipient { Amount = amount, ScriptPubKey = destination } }
            };

            // Avoid errors being raised due to the special script that we are using.
            context.TransactionBuilder.StandardTransactionPolicy.CheckScriptPubKey = false;

            Transaction transaction = this.walletTransactionHandler.BuildTransaction(context);

            this.logger.LogTrace("(-):'{0}'", transaction.GetHash());
            return transaction;
        }

        /// <summary>
        /// Creates a cold staking withdrawal <see cref="Transaction"/>.
        /// </summary>
        /// <remarks>
        /// Cold staking withdrawal can only be performed on the wallet that is in the role of the cold staking cold wallet.
        /// </remarks>
        /// <param name="receivingAddress">The address that will receive the withdrawal.</param>
        /// <param name="walletName">The name of the wallet in the role of cold wallet.</param>
        /// <param name="walletPassword">The wallet password.</param>
        /// <param name="amount">The amount to remove from cold staking.</param>
        /// <param name="feeAmount">The fee to pay for cold staking transaction withdrawal.</param>
        /// <returns>The <see cref="Transaction"/> for cold staking withdrawal.</returns>
        /// <exception cref="WalletException">Thrown if any of the rules listed in the remarks section of this method are broken.</exception>
        internal Transaction GetColdStakingWithdrawalTransaction(string receivingAddress,
            string walletName, string walletPassword, Money amount, Money feeAmount)
        {
            Guard.NotEmpty(receivingAddress, nameof(receivingAddress));
            Guard.NotEmpty(walletName, nameof(walletName));
            Guard.NotNull(amount, nameof(amount));
            Guard.NotNull(feeAmount, nameof(feeAmount));

            this.logger.LogTrace("({0}:'{1}',{2}:'{3}',{4}:'{5}',{6}:'{7}'",
                nameof(receivingAddress), receivingAddress,
                nameof(walletName), walletName,
                nameof(amount), amount,
                nameof(feeAmount), feeAmount
                );

            Wallet.Wallet wallet = this.WalletManager.GetWalletByName(walletName);

            // Get/create the cold staking account.
            HdAccount coldAccount = this.CreateColdStakingAccount(walletName, true, walletPassword);
            if (coldAccount == null)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_ACCOUNT_DOES_NOT_EXIST]");
                throw new WalletException("The cold wallet account does not exist.");
            }

            // The receivingAddress can't be a cold staking account.
            bool isColdStakingAddress = coldAccount.ExternalAddresses.Concat(coldAccount.InternalAddresses).Select(a => a.Address.ToString()).Contains(receivingAddress);
            if (!isColdStakingAddress)
            {
                HdAccount hotAccount = this.GetColdStakingAccount(wallet, false);
                if (hotAccount != null)
                    isColdStakingAddress = hotAccount.ExternalAddresses.Concat(hotAccount.InternalAddresses).Select(a => a.Address.ToString()).Contains(receivingAddress);
            }

            if (isColdStakingAddress)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_INVALID_RECEIVING_ADDRESS]");
                throw new WalletException("You can't send the money to a cold staking account.");
            }

            // Send the money to the receiving address.
            Script destination = BitcoinAddress.Create(receivingAddress, wallet.Network).ScriptPubKey.PaymentScript;

            // Take the largest unspent outputs from the specified cold staking or setup transaction.
            Money availAmt = 0;
            List<UnspentOutputReference> unspents = this.WalletManager
                .GetSpendableTransactionsInAccount(new WalletAccountReference(walletName, coldAccount.Name))
                .OrderByDescending(a => a.Transaction.Amount)
                .TakeWhile(a => (availAmt += a.Transaction.Amount) < amount).ToList();

            // Check the amount requested against the amount available.
            if (availAmt < amount)
            {
                this.logger.LogTrace("(-)[COLDSTAKE_AMOUNT_TOO_LARGE]");
                throw new WalletException("Insufficient balance amount.");
            }

            var context = new TransactionBuildContext(wallet.Network)
            {
                AccountReference = new WalletAccountReference(walletName, coldAccount.Name),
                SelectedInputs = unspents.Select(a => a.ToOutPoint()).ToList(),
                ChangeAddress = unspents[0].Address,
                TransactionFee = feeAmount,
                MinConfirmations = 0,
                Shuffle = false,
                WalletPassword = walletPassword,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList()
            };

            // Avoid errors being raised due to the special script that we are using.
            context.TransactionBuilder.StandardTransactionPolicy.CheckScriptPubKey = false;

            // Build the transaction.
            Transaction transaction = this.WalletTransactionHandler.BuildTransaction(context);

            this.logger.LogTrace("(-)");
            return transaction;
        }
    }
}
