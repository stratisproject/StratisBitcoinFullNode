using System.Collections.Generic;
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
        private IFederationWalletManager federationWalletManager2;
        private IFederationWalletTransactionHandler federationTransactionHandler;
        private ConcurrentChain chain;
        private NodeSettings nodeSettings;
        private DataFolder dataFolder;
        private IWalletFeePolicy walletFeePolicy;
        private FederationWallet wallet;

        public TransactionBuilderTests()
        {
            this.network = ApexNetwork.RegTest;
            this.nodeSettings = new NodeSettings(this.network, NBitcoin.Protocol.ProtocolVersion.ALT_PROTOCOL_VERSION,
                args: new[] {
                    "-mainchain",
                    "-redeemscript=2 026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c 02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687 02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1 03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4 034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38 5 OP_CHECKMULTISIG",
                    "-publickey=026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c"
                });
            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.settings = new FederationGatewaySettings(this.nodeSettings);
            this.chain = new ConcurrentChain(this.network);
            this.dataFolder = this.nodeSettings.DataFolder;
            var mockWalletFeePolicy = new Mock<IWalletFeePolicy>();
            this.walletFeePolicy = mockWalletFeePolicy.Object;
            this.dateTimeProvider = DateTimeProvider.Default;

            // Create the wallet manager.
            this.federationWalletManager = new FederationWalletManager(
                this.loggerFactory,
                this.network,
                this.chain,
                this.nodeSettings,
                this.dataFolder,
                this.walletFeePolicy,
                new Mock<IAsyncLoopFactory>().Object,
                new NodeLifetime(),
                this.dateTimeProvider,
                this.settings);

            // Create the wallet.
            this.wallet = (this.federationWalletManager as FederationWalletManager).GenerateWallet();

            var mockFederationWalletManager = new Mock<IFederationWalletManager>();

            // Return the mock wallet.
            mockFederationWalletManager.Setup(s => s.GetWallet()).Returns(this.wallet);

            // Mock funds in wallet.
            mockFederationWalletManager.Setup(s => s.GetSpendableTransactionsInWallet(It.IsAny<int>())).Returns(new[]
            {
                new UnspentOutputReference()
                {
                    Transaction = new TransactionData()
                    {
                        Amount = Money.COIN * 90,
                        Id = new uint256(1),
                        Index = 0,
                        ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                        BlockHeight = 2
                    }
                },
                new UnspentOutputReference()
                {
                    Transaction = new TransactionData()
                    {
                        Amount = Money.COIN * 80,
                        Id = new uint256(1),
                        Index = 1,
                        ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                        BlockHeight = 2
                    }
                },
                new UnspentOutputReference()
                {
                    Transaction = new TransactionData()
                    {
                        Amount = Money.COIN * 70,
                        Id = new uint256(2),
                        Index = 0,
                        ScriptPubKey = this.wallet.MultiSigAddress.ScriptPubKey,
                        BlockHeight = 3
                    }
                }
            });

            this.federationWalletManager2 = mockFederationWalletManager.Object;
            this.federationTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager2, this.walletFeePolicy, this.network);
        }

        [Fact]
        public void BuildTransactionBuildsDeterministicTransactions()
        {
            MultiSigAddress multiSigAddress = this.wallet.MultiSigAddress;
            var recipients = new List<Recipient.Recipient>()
            {
                new Recipient.Recipient
                {
                    Amount = Money.COIN * 160,
                    ScriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new Key().PubKey.Hash)
                }
            };

            int blockHeight = this.chain.Height;
            string opReturnData = blockHeight.ToString();

            // Build the multisig transaction template.
            var multiSigContext = new TransactionBuildContext(recipients, opReturnData: Encoding.UTF8.GetBytes(opReturnData))
            {
                TransactionFee = Money.Coins(0.01m),
                MinConfirmations = 1,
                Shuffle = false,
                MultiSig = multiSigAddress,
                IgnoreVerify = true,
                Sign = false
            };

            // Build the transaction.
            Transaction transaction = this.federationTransactionHandler.BuildTransaction(multiSigContext);

            // Transactions inputs.
            Assert.Equal(2, transaction.Inputs.Count);
            Assert.Equal((uint256)1, transaction.Inputs[0].PrevOut.Hash);
            Assert.Equal((uint)0, transaction.Inputs[0].PrevOut.N);
            Assert.Equal((uint256)1, transaction.Inputs[1].PrevOut.Hash);
            Assert.Equal((uint)1, transaction.Inputs[1].PrevOut.N);

            // Transaction outputs.
            Assert.Equal(3, transaction.Outputs.Count);

            // Transaction output value - change.
            Assert.Equal(new Money(9.99m, MoneyUnit.BTC), transaction.Outputs[0].Value);
            Assert.Equal(multiSigAddress.ScriptPubKey, transaction.Outputs[0].ScriptPubKey);

            // Transaction output value - recipient.
            Assert.Equal(new Money(160m, MoneyUnit.BTC), transaction.Outputs[1].Value);
            Assert.Equal(recipients[0].ScriptPubKey, transaction.Outputs[1].ScriptPubKey);

            // Transaction output value - op_return.
            Assert.Equal(new Money(0m, MoneyUnit.BTC), transaction.Outputs[2].Value);
            Assert.Equal(opReturnData, new OpReturnDataReader(this.loggerFactory, this.network).GetString(transaction, out OpReturnDataType dummy));
        }
    }
}
