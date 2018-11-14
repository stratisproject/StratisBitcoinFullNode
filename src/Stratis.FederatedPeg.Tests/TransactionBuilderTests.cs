using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using Stratis.Sidechains.Networks;
using Xunit;
using Recipient = Stratis.FederatedPeg.Features.FederationGateway.Wallet;

namespace Stratis.FederatedPeg.Tests
{
    /// <summary>
    /// Tests whether transactions are built deterministically.
    /// </summary>
    public class TransactionBuilderTests
    {
        private Network network;
        private IDateTimeProvider dateTimeProvider;
        private ILoggerFactory loggerFactory;
        private IFederationGatewaySettings settings;
        private IFederationWalletManager federationWalletManager;
        private IFederationWalletTransactionHandler federationTransactionHandler;
        private ConcurrentChain chain;
        private IWalletFeePolicy walletFeePolicy;
        private FederationWallet wallet;

        public TransactionBuilderTests()
        {
            this.network = ApexNetwork.RegTest;
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.loggerFactory = Substitute.For<ILoggerFactory>();

            this.settings = Substitute.For<IFederationGatewaySettings>();
            var redeemScript = new Script("2 026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c 02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687 02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1 03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4 034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38 5 OP_CHECKMULTISIG");
            this.settings.IsMainChain.Returns(false);
            this.settings.MultiSigRedeemScript.Returns(redeemScript);
            this.settings.MultiSigAddress.Returns(redeemScript.Hash.GetAddress(this.network));
            this.settings.PublicKey.Returns("026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c");

            this.chain = new ConcurrentChain(this.network);
            var mockWalletFeePolicy = new Mock<IWalletFeePolicy>();
            this.walletFeePolicy = mockWalletFeePolicy.Object;
            this.dateTimeProvider = DateTimeProvider.Default;

            // Create the wallet manager.
            this.federationWalletManager = new FederationWalletManager(
                this.loggerFactory,
                this.network,
                this.chain,
                dataFolder,
                this.walletFeePolicy,
                new Mock<IAsyncLoopFactory>().Object,
                new NodeLifetime(),
                this.dateTimeProvider,
                this.settings);

            // Create the wallet.
            this.wallet = (this.federationWalletManager as FederationWalletManager).GenerateWallet();
            (this.federationWalletManager as FederationWalletManager).Wallet = this.wallet;

            this.wallet.MultiSigAddress.Transactions.Add(new TransactionData()
            {
                Amount = Money.COIN * 90,
                Id = new uint256(1),
                Index = 0,
                ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                BlockHeight = 2
            });

            this.wallet.MultiSigAddress.Transactions.Add(new TransactionData()
            {
                Amount = Money.COIN * 80,
                Id = new uint256(1),
                Index = 1,
                ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                BlockHeight = 2
            });

            this.wallet.MultiSigAddress.Transactions.Add(new TransactionData()
            {
                Amount = Money.COIN * 70,
                Id = new uint256(2),
                Index = 0,
                ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                BlockHeight = 2
            });

            (this.federationWalletManager as FederationWalletManager).LoadKeysLookupLock();

            this.federationTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager, this.walletFeePolicy, this.network);
        }

        [Fact]
        public void BuildTransactionBuildsDeterministicTransactions()
        {
            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;

            var recipient1 = new List<Recipient.Recipient>()
            {
                new Recipient.Recipient
                {
                    Amount = Money.COIN * 160,
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey.Hash)
                }
            };

            int blockHeight = this.chain.Height;

            string opReturnData1 = blockHeight.ToString();

            // Build the multisig transaction template.
            var multiSigContext1 = new TransactionBuildContext(recipient1, opReturnData: Encoding.UTF8.GetBytes(opReturnData1))
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 0,
                Shuffle = false,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            // Build the transaction.
            Transaction transaction1 = this.federationTransactionHandler.BuildTransaction(multiSigContext1);

            // Transactions inputs.
            Assert.Equal(2, transaction1.Inputs.Count);
            Assert.Equal((uint256)1, transaction1.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction1.Inputs[0].PrevOut.N);
            Assert.Equal((uint256)1, transaction1.Inputs[1].PrevOut.Hash);
            Assert.Equal((uint)1, transaction1.Inputs[1].PrevOut.N);

            // Transaction outputs.
            Assert.Equal(3, transaction1.Outputs.Count);

            // Transaction output value - change.
            Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transaction1.Outputs[0].Value);
            Assert.Equal(multiSigAddress.ScriptPubKey, transaction1.Outputs[0].ScriptPubKey);

            // Transaction output value - recipient 1.
            Assert.Equal(new Money(160m, MoneyUnit.BTC), transaction1.Outputs[1].Value);
            Assert.Equal(recipient1[0].ScriptPubKey, transaction1.Outputs[1].ScriptPubKey);

            // Transaction output value - op_return.
            Assert.Equal(new Money(0m, MoneyUnit.BTC), transaction1.Outputs[2].Value);
            Assert.Equal(opReturnData1, new OpReturnDataReader(this.loggerFactory, this.network).GetString(transaction1, out OpReturnDataType dummy));

            // Reserve UTXO's spent by transaction 1.
            this.federationWalletManager.ProcessTransaction(transaction1);

            // All the input UTXO's should be present in spending details of the multi-sig address.
            Assert.True(CrossChainTransferStore.SanityCheck(transaction1, this.wallet));

            // Confirm this can be done twice without ill effect.
            // (We may have to re-reserve UTXO's associated with partial transactions after a node restart or re-org.)
            this.federationWalletManager.ProcessTransaction(transaction1);

            var recipient2 = new List<Recipient.Recipient>()
            {
                new Recipient.Recipient
                {
                    Amount = Money.COIN * 70,
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey.Hash)
                }
            };

            string opReturnData2 = blockHeight.ToString();

            // Build the multisig transaction template.
            var multiSigContext2 = new TransactionBuildContext(recipient2, opReturnData: Encoding.UTF8.GetBytes(opReturnData2))
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 0,
                Shuffle = false,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            // Build the transaction.
            Transaction transaction2 = this.federationTransactionHandler.BuildTransaction(multiSigContext2);

            // Transactions inputs.
            Assert.Equal(2, transaction2.Inputs.Count);
            Assert.Equal(transaction1.GetHash(), transaction2.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction2.Inputs[0].PrevOut.N);
            Assert.Equal((uint256)2, transaction2.Inputs[1].PrevOut.Hash);
            Assert.Equal((uint)0, transaction2.Inputs[1].PrevOut.N);

            // Transaction outputs.
            Assert.Equal(3, transaction2.Outputs.Count);

            // Transaction output value - change.
            Assert.Equal(new Money(9.98m, MoneyUnit.BTC), transaction2.Outputs[0].Value);
            Assert.Equal(multiSigAddress.ScriptPubKey, transaction2.Outputs[0].ScriptPubKey);

            // Transaction output value - recipient 2.
            Assert.Equal(new Money(70m, MoneyUnit.BTC), transaction2.Outputs[1].Value);
            Assert.Equal(recipient2[0].ScriptPubKey, transaction2.Outputs[1].ScriptPubKey);

            // Transaction output value - op_return.
            Assert.Equal(new Money(0m, MoneyUnit.BTC), transaction2.Outputs[2].Value);
            Assert.Equal(opReturnData2, new OpReturnDataReader(this.loggerFactory, this.network).GetString(transaction2, out OpReturnDataType dummy2));

            // Reserve UTXO's spent by transaction 2.
            this.federationWalletManager.ProcessTransaction(transaction2);

            // All the input UTXO's should be present in spending details of the multi-sig address.
            Assert.True(CrossChainTransferStore.SanityCheck(transaction2, this.wallet));

        }

        /// <summary>
        /// Creates a directory for a test, based on the name of the class containing the test and the name of the test.
        /// </summary>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory that was created.</returns>
        public static string CreateTestDir(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string directoryPath = GetTestDirectoryPath(caller, callingMethod);
            return AssureEmptyDir(directoryPath);
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> or <see cref="CreateDataFolder(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="caller">The calling object, from which we derive the namespace in which the test is contained.</param>
        /// <param name="callingMethod">The name of the test being executed. A directory with the same name will be created.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            return GetTestDirectoryPath(Path.Combine(caller.GetType().Name, callingMethod));
        }

        /// <summary>
        /// Gets the path of the directory that <see cref="CreateTestDir(object, string)"/> would create.
        /// </summary>
        /// <remarks>The path of the directory is of the form TestCase/{testClass}/{testName}.</remarks>
        /// <param name="testDirectory">The directory in which the test files are contained.</param>
        /// <returns>The path of the directory.</returns>
        public static string GetTestDirectoryPath(string testDirectory)
        {
            return Path.Combine("..", "..", "..", "..", "TestCase", testDirectory);
        }

        public static string AssureEmptyDir(string dir)
        {
            string uniqueDirName = $"{dir}-{DateTime.UtcNow:ddMMyyyyTHH.mm.ss.fff}";
            Directory.CreateDirectory(uniqueDirName);
            return uniqueDirName;
        }
    }
}
