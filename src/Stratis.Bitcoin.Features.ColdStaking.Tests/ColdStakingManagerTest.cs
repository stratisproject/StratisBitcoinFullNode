using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.SQLiteWalletRepository;
using Xunit;

namespace Stratis.Bitcoin.Features.ColdStaking.Tests
{
    public class ColdStakingManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        private readonly IBlockStore blockStore;
        private readonly WalletFixture walletFixture;

        public ColdStakingManagerTest(WalletFixture walletFixture)
        {
            this.blockStore = new Mock<IBlockStore>().Object;
            this.walletFixture = walletFixture;
            this.Network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);
        }

        public Transaction CreateColdStakingSetupTransaction(Wallet.Wallet wallet, string password, HdAddress spendingAddress, PubKey destinationColdPubKey, PubKey destinationHotPubKey, HdAddress changeAddress, Money amount, Money fee)
        {
            TransactionData spendingTransaction = spendingAddress.Transactions.ElementAt(0);
            var coin = new Coin(spendingTransaction.Id, (uint)spendingTransaction.Index, spendingTransaction.Amount, spendingTransaction.ScriptPubKey);

            Key privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);

            Script script = ColdStakingScriptTemplate.Instance.GenerateScriptPubKey(destinationHotPubKey.Hash, destinationColdPubKey.Hash);

            var builder = new TransactionBuilder(wallet.Network);
            builder.Extensions.Add(new ColdStakingBuilderExtension(false));
            Transaction tx = builder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(new ExtKey(privateKey, wallet.ChainCode).Derive(new KeyPath(spendingAddress.HdPath)).GetWif(wallet.Network))
                .Send(script, amount)
                .SetChange(changeAddress.ScriptPubKey)
                .SendFees(fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return tx;
        }

        public Transaction CreateColdStakingWithdrawalTransaction(Wallet.Wallet wallet, string password, HdAddress spendingAddress, PubKey destinationPubKey, Script changeScript, Money amount, Money fee)
        {
            TransactionData spendingTransaction = spendingAddress.Transactions.ElementAt(0);
            var coin = new Coin(spendingTransaction.Id, (uint)spendingTransaction.Index, spendingTransaction.Amount, spendingTransaction.ScriptPubKey);

            Key privateKey = Key.Parse(wallet.EncryptedSeed, password, wallet.Network);

            Script script = destinationPubKey.ScriptPubKey;

            var builder = new TransactionBuilder(wallet.Network);
            builder.Extensions.Add(new ColdStakingBuilderExtension(false));
            Transaction tx = builder
                .AddCoins(new List<Coin> { coin })
                .AddKeys(new ExtKey(privateKey, wallet.ChainCode).Derive(new KeyPath(spendingAddress.HdPath)).GetWif(wallet.Network))
                .Send(script, amount)
                .SetChange(changeScript)
                .SendFees(fee)
                .BuildTransaction(true);

            if (!builder.Verify(tx))
            {
                throw new WalletException("Could not build transaction, please make sure you entered the correct data.");
            }

            return tx;
        }

        /// <summary>
        /// Creates a spendable transaction and spends its outputs to a cold staking script.
        /// Tests whether the original wallet, hot wallet and cold wallet record the expected information.
        /// </summary>
        [Fact]
        public void ProcessTransactionWithValidColdStakingSetupLoadsTransactionsIntoWalletIfMatching()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            Directory.CreateDirectory(dataFolder.WalletPath);

            var chain = new ChainIndexer(this.Network);

            var walletFeePolicy = new Mock<IWalletFeePolicy>();
            walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
                .Returns(new Money(5000));

            var walletSettings = new WalletSettings(new NodeSettings(network: this.Network));
            IScriptAddressReader scriptAddressReader = new ColdStakingDestinationReader(new ScriptAddressReader());
            var walletRepository = new SQLiteWalletRepository(this.LoggerFactory.Object, dataFolder, this.Network, DateTimeProvider.Default, scriptAddressReader);
            walletRepository.TestMode = true;
            var walletManager = new ColdStakingManager(this.Network, chain, walletSettings, dataFolder, walletFeePolicy.Object,
                new Mock<IAsyncProvider>().Object, new NodeLifetime(), new ScriptAddressReader(), this.LoggerFactory.Object, DateTimeProvider.Default,
                walletRepository, new Mock<IBroadcasterManager>().Object);

            walletManager.Start();

            Wallet.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet", "password", walletRepository);
            Wallet.Wallet coldWallet = this.walletFixture.GenerateBlankWallet("myColdWallet", "password", walletRepository);
            Wallet.Wallet hotWallet = this.walletFixture.GenerateBlankWallet("myHotWallet", "password", walletRepository);
            Wallet.Wallet withdrawalWallet = this.walletFixture.GenerateBlankWallet("myWithDrawalWallet", "password", walletRepository);

            HdAccount withdrawalAccount = withdrawalWallet.AddNewAccount("password");
            HdAddress withdrawalAddress = withdrawalAccount.ExternalAddresses.ElementAt(0);
            PubKey withdrawalPubKey = withdrawalAddress.Pubkey.GetDestinationPublicKeys(this.Network)[0];

            HdAccount account = wallet.AddNewAccount("password");
            HdAddress changeAddress = account.InternalAddresses.ElementAt(0);
            HdAddress spendingAddress = account.ExternalAddresses.ElementAt(0);

            HdAccount coldWalletAccount = coldWallet.AddNewAccount("password", ColdStakingManager.ColdWalletAccountIndex, ColdStakingManager.ColdWalletAccountName);
            HdAddress destinationColdAddress = coldWalletAccount.ExternalAddresses.ElementAt(0);
            PubKey destinationColdPubKey = destinationColdAddress.Pubkey.GetDestinationPublicKeys(this.Network)[0];

            HdAccount hotWalletAccount = hotWallet.AddNewAccount("password", ColdStakingManager.HotWalletAccountIndex, ColdStakingManager.HotWalletAccountName);
            HdAddress destinationHotAddress = hotWalletAccount.ExternalAddresses.ElementAt(0);
            PubKey destinationHotPubKey = destinationHotAddress.Pubkey.GetDestinationPublicKeys(this.Network)[0];

            // Generate a spendable transaction
            (uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateFirstBlockWithPaymentToAddress(chain, wallet.Network, spendingAddress);

            walletManager.ProcessBlock(chainInfo.block);

            // Create a cold staking setup transaction.
            Transaction transaction = this.CreateColdStakingSetupTransaction(wallet, "password", spendingAddress, destinationColdPubKey, destinationHotPubKey,
                changeAddress, new Money(7500), new Money(5000));

            walletManager.ProcessBlock(WalletTestsHelpers.AppendTransactionInNewBlockToChain(chain, transaction));

            HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Assert.Single(spendingAddress.Transactions);
            Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
            Assert.Equal(2, transaction.Outputs.Count);
            Assert.True(spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Any(p => p.DestinationScriptPubKey == transaction.Outputs[1].ScriptPubKey && p.Amount == transaction.Outputs[1].Value));

            Assert.Single(wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions);
            TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
            Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
            Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

            // Verify that the transaction has been recorded in the cold wallet.
            Assert.Single(coldWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions);
            TransactionData destinationColdAddressResult = coldWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationColdAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationColdAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationColdAddressResult.ScriptPubKey);

            // Verify that the transaction has been recorded in the hot wallet.
            Assert.Single(hotWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions);
            TransactionData destinationHotAddressResult = hotWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.ElementAt(0);
            Assert.Equal(transaction.GetHash(), destinationHotAddressResult.Id);
            Assert.Equal(transaction.Outputs[1].Value, destinationHotAddressResult.Amount);
            Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationHotAddressResult.ScriptPubKey);

            // Will spend from the cold stake address and send the change back to the same address.
            Money balance = walletManager.GetSpendableTransactionsInAccount(new WalletAccountReference(coldWallet.Name, coldWalletAccount.Name), 0).Sum(x => x.Transaction.Amount);
            var coldStakeAddress = coldWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
            Transaction withdrawalTransaction = this.CreateColdStakingWithdrawalTransaction(coldWallet, "password", coldStakeAddress,
                withdrawalPubKey, ColdStakingScriptTemplate.Instance.GenerateScriptPubKey(destinationColdPubKey.Hash, destinationHotPubKey.Hash),
                new Money(750), new Money(263));

            // Process the transaction.
            walletManager.ProcessBlock(WalletTestsHelpers.AppendTransactionInNewBlockToChain(chain, withdrawalTransaction));

            // Verify that the transaction has been recorded in the withdrawal wallet.
            Assert.Single(withdrawalWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions);
            TransactionData withdrawalAddressResult = withdrawalWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions
                .Where(t => t.Id == withdrawalTransaction.GetHash()).First();
            Assert.Equal(withdrawalTransaction.GetHash(), withdrawalAddressResult.Id);
            Assert.Equal(withdrawalTransaction.Outputs[1].Value, withdrawalAddressResult.Amount);
            Assert.Equal(withdrawalTransaction.Outputs[1].ScriptPubKey, withdrawalAddressResult.ScriptPubKey);

            // Verify that the transaction has been recorded in the cold wallet.
            Assert.Equal(2, coldWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Count);
            TransactionData coldAddressResult = coldWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions
                .Where(t => t.Id == withdrawalTransaction.GetHash()).First();
            Assert.Equal(withdrawalTransaction.GetHash(), coldAddressResult.Id);
            Assert.Equal(withdrawalTransaction.Outputs[0].Value, coldAddressResult.Amount);
            Assert.Equal(withdrawalTransaction.Outputs[0].ScriptPubKey, coldAddressResult.ScriptPubKey);

            // Verify that the transaction has been recorded in the hot wallet.
            Assert.Equal(2, hotWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.Count);
            TransactionData hotAddressResult = hotWallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions
                .Where(t => t.Id == withdrawalTransaction.GetHash()).First();
            Assert.Equal(withdrawalTransaction.GetHash(), hotAddressResult.Id);
            Assert.Equal(withdrawalTransaction.Outputs[0].Value, hotAddressResult.Amount);
            Assert.Equal(withdrawalTransaction.Outputs[0].ScriptPubKey, hotAddressResult.ScriptPubKey);

            // Verify the hot amount returned by GetBalances.
            AccountBalance hotBalance = walletManager.GetBalances("myHotWallet", ColdStakingManager.HotWalletAccountName).FirstOrDefault();
            Assert.Equal(hotBalance.AmountConfirmed, hotAddressResult.Amount);

            // Verify the cold amount returned by GetBalances.
            AccountBalance coldBalance = walletManager.GetBalances("myColdWallet", ColdStakingManager.ColdWalletAccountName).FirstOrDefault();
            Assert.Equal(coldBalance.AmountConfirmed, coldAddressResult.Amount);
        }
    }
}
