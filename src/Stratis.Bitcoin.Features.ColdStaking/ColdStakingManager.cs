using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// The manager class for implementing cold staking.
    /// </summary>
    /// <seealso cref="ColdStakingFeature"/>
    public class ColdStakingManager
    {
        /// <summary>The account index of the cold wallet account.</summary>
        private const int ColdWalletAccountIndex = Wallet.Wallet.ColdStakingAccountIndex + 0;

        /// <summary>The account index of the hot wallet account.</summary>
        private const int HotWalletAccountIndex = Wallet.Wallet.ColdStakingAccountIndex + 1;

        /// <summary>The wallet manager to use for accessing wallets and their accounts.</summary>
        public IWalletManager WalletManager { get; private set; }

        /// <summary>The wallet transaction handler to use for building transactions.</summary>
        public IWalletTransactionHandler WalletTransactionHandler { get; private set; }

        /// <summary>The wallet broadcast manager to use for broadcasting transactions.</summary>
        public IBroadcasterManager BroadcasterManager { get; private set; }

        /// <summary>
        /// Constructs the cold staking manager which is used by the cold staking controller.
        /// </summary>
        /// <param name="walletManager">The wallet manager to use for accessing wallets and their accounts.</param>
        /// <param name="walletTransactionHandler">The wallet transaction handler to use for building transactions.</param>
        /// <param name="broadcasterManager">The wallet broadcast manager to use for broadcasting transactions.</param>
        /// <param name="network">Specification of the network the node runs on.</param>
        public ColdStakingManager(IWalletManager walletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IBroadcasterManager broadcasterManager)
        {
            this.WalletManager = walletManager;
            this.WalletTransactionHandler = walletTransactionHandler;
            this.BroadcasterManager = broadcasterManager;
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
        /// <param name="walletPassword">The wallet password.</param>
        /// <param name="isColdWalletAccount">Indicates whether we need the cold wallet account (versus the hot wallet account).</param>
        /// <param name="createIfNotExists">Indicates whether to create the account if it does not exist.</param>
        /// <returns>The cold staking account.</returns>
        public HdAccount GetColdStakingAccount(Wallet.Wallet wallet, string walletPassword,
            bool isColdWalletAccount, bool createIfNotExists = true)
        {
            int accountIndex = isColdWalletAccount ? ColdWalletAccountIndex : HotWalletAccountIndex;

            CoinType coinType = (CoinType)wallet.Network.Consensus.CoinType;

            HdAccount account = wallet.GetAccountsByCoinType(coinType).Where(a => a.Index == accountIndex).FirstOrDefault();

            if (account == null)
            {
                if (!createIfNotExists)
                    return null;

                AccountRoot accountRoot = wallet.AccountsRoot.Single(a => a.CoinType == coinType);

                account = accountRoot.CreateAccount(walletPassword, wallet.EncryptedSeed,
                    wallet.ChainCode, wallet.Network, DateTimeOffset.UtcNow, accountIndex);

                ICollection<HdAccount> hdAccounts = accountRoot.Accounts.ToList();
                hdAccounts.Add(account);
                accountRoot.Accounts = hdAccounts;
            }

            if (account.ExternalAddresses.Count == 0)
                account.CreateAddresses(wallet.Network, 1, false);

            return account;
        }

        /// <summary>
        /// Gets a cold staking address.
        /// </summary>
        /// <param name="wallet">The wallet.</param>
        /// <param name="walletPassword">The wallet password.</param>
        /// <param name="isColdWalletAddress">Indicates whether we need the cold wallet address (versus the hot wallet address).</param>
        /// <returns>The cold staking address.</returns>
        public HdAddress GetColdStakingAddress(Wallet.Wallet wallet, string walletPassword, bool isColdWalletAddress)
        {
            return GetColdStakingAccount(wallet, walletPassword, isColdWalletAddress).ExternalAddresses.First();
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
        /// opcode which does the following checks:
        /// <list type="number">
        /// <item>Check if the transaction spending an output, which contains this instruction, is a coinstake
        /// transaction. If it is not, the script fails.</item>
        /// <item>Check that ScriptPubKeys of all inputs of this transaction are the same. If they are not,
        /// the script fails.</item>
        /// <item>Check that ScriptPubKeys of all outputs of this transaction, except for the marker output (a
        /// special first output of each coinstake transaction) and the pubkey output (an optional special
        /// second output that contains public key in coinstake transaction), are the same as ScriptPubKeys of
        /// the inputs. If they are not, the script fails.</item>
        /// <item>Check that the sum of values of all inputs is smaller or equal to the sum of values of all
        /// outputs. If this does not hold, the script fails.</item>
        /// <item>If the above-mentioned checks pass, the instruction does nothing.</item>
        /// </list>
        /// </remarks>
        /// <param name="hotPubKey">The "hotPubKey" to use.</param>
        /// <param name="coldPubKey">The "coldPubKey" to use.</param>
        /// <returns>The cold staking script.</returns>
        private static Script GetColdStakingScript(ScriptId hotPubKey, ScriptId coldPubKey)
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
        public TransactionBuildContext GetSetupBuildContext(
            string coldWalletAddress, string hotWalletAddress, string walletName, string walletAccount,
            string walletPassword, Money amount, Money feeAmount)
        {
            Wallet.Wallet wallet = this.WalletManager.GetWalletByName(walletName);
            CoinType coinType = (CoinType)wallet.Network.Consensus.CoinType;
            HdAccount coldAccount = this.GetColdStakingAccount(wallet, walletPassword, true, false);
            HdAccount hotAccount = this.GetColdStakingAccount(wallet, walletPassword, false, false);

            bool thisIsColdWallet = coldAccount?.ExternalAddresses.Select(a => a.Address).Contains(coldWalletAddress) ?? false;
            bool thisIsHotWallet = hotAccount?.ExternalAddresses.Select(a => a.Address).Contains(hotWalletAddress) ?? false;

            if (thisIsColdWallet && thisIsHotWallet)
                throw new WalletException("You can't use this wallet as both hot wallet and cold wallet.");

            if (!thisIsColdWallet && !thisIsHotWallet)
                throw new WalletException("The hot and cold wallet addresses could not be found in the corresponding accounts.");

            ScriptId hotPubKey = BitcoinAddress.Create(hotWalletAddress, wallet.Network).ScriptPubKey.Hash;
            ScriptId coldPubKey = BitcoinAddress.Create(coldWalletAddress, wallet.Network).ScriptPubKey.Hash;
            Script destination = GetColdStakingScript(hotPubKey, coldPubKey);

            // Only normal accounts should be allowed.
            if (this.WalletManager.GetAccounts(walletName).Where(a => a.Name == walletAccount).Single().Index >= ColdWalletAccountIndex)
                throw new WalletException($"You can't perform this operation with wallet account '{ walletAccount }'");

            return new TransactionBuildContext(wallet.Network)
            {
                AccountReference = new WalletAccountReference(walletName, walletAccount),
                TransactionFee = feeAmount,
                MinConfirmations = 0,
                Shuffle = true,
                OpReturnData = (thisIsHotWallet ? hotPubKey : coldPubKey).ToString(),
                WalletPassword = walletPassword,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destination } }.ToList()
            };
        }
    }
}
