using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NSubstitute;
using Stratis.Bitcoin.Configuration;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.TargetChain;
using Xunit;
using Stratis.Sidechains.Networks;
using Stratis.FederatedPeg.Features.FederationGateway;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.FederatedPeg.Features.FederationGateway.SourceChain;
using Stratis.FederatedPeg.Features.FederationGateway.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.FederatedPeg.Tests
{
    public class CrossChainTransferStoreTests
    {
        private Network network;
        private ILoggerFactory loggerFactory;
        private ILogger logger;
        private IDateTimeProvider dateTimeProvider;
        private IOpReturnDataReader opReturnDataReader;
        private IBlockRepository blockRepository;
        private IFullNode fullNode;
        private IFederationWalletManager federationWalletManager;
        private IFederationWalletTransactionHandler federationWalletTransactionHandler;
        private IFederationGatewaySettings federationGatewaySettings;
        private IFederationWalletSyncManager federationWalletSyncManager;
        private IWalletFeePolicy walletFeePolicy;
        private IAsyncLoopFactory asyncLoopFactory;
        private Dictionary<uint256, Block> blockDict;
        private ConcurrentChain chain;
        private FederationWallet wallet;

        /// <summary>
        /// Initializes the cross-chain transfer tests.
        /// </summary>
        public CrossChainTransferStoreTests()
        {
            this.network = ApexNetwork.RegTest;

            DBreezeSerializer serializer = new DBreezeSerializer();
            serializer.Initialize(this.network);

            this.loggerFactory = Substitute.For<ILoggerFactory>();
            this.logger = Substitute.For<ILogger>();
            this.asyncLoopFactory = Substitute.For<IAsyncLoopFactory>();
            this.loggerFactory.CreateLogger(null).ReturnsForAnyArgs(this.logger);
            this.dateTimeProvider = DateTimeProvider.Default;
            this.opReturnDataReader = new OpReturnDataReader(this.loggerFactory, this.network);
            this.blockRepository = Substitute.For<IBlockRepository>();
            this.fullNode = Substitute.For<IFullNode>();
            this.federationWalletManager = Substitute.For<IFederationWalletManager>();
            this.federationWalletTransactionHandler = Substitute.For<IFederationWalletTransactionHandler>();
            this.federationWalletSyncManager = Substitute.For<IFederationWalletSyncManager>();
            this.walletFeePolicy = Substitute.For<IWalletFeePolicy>();
            this.wallet = null;
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            var redeemScript = new Script("2 026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c 02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687 02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1 03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4 034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38 5 OP_CHECKMULTISIG");
            this.federationGatewaySettings.IsMainChain.Returns(false);
            this.federationGatewaySettings.MultiSigRedeemScript.Returns(redeemScript);
            this.federationGatewaySettings.MultiSigAddress.Returns(redeemScript.Hash.GetAddress(this.network));
            this.federationGatewaySettings.PublicKey.Returns("026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c");

            this.blockDict = new Dictionary<uint256, Block>();

            this.blockRepository.GetBlocksAsync(Arg.Any<List<uint256>>()).ReturnsForAnyArgs((x) => {
                List<uint256> hashes = x.ArgAt<List<uint256>>(0);
                var blocks = new List<Block>();
                for (int i = 0; i < hashes.Count; i++)
                {
                    blocks.Add(this.blockDict.TryGetValue(hashes[i], out Block block) ? block : null);
                }

                return blocks;
            });
        }

        private void CreateWalletManagerAndTransactionHandler(ConcurrentChain chain, DataFolder dataFolder)
        {
            // Create the wallet manager.
            this.federationWalletManager = new FederationWalletManager(
                this.loggerFactory,
                this.network,
                chain,
                dataFolder,
                this.walletFeePolicy,
                this.asyncLoopFactory,
                new NodeLifetime(),
                this.dateTimeProvider,
                this.federationGatewaySettings);

            // Starts and creates the wallet.
            this.federationWalletManager.Start();
            this.wallet = this.federationWalletManager.GetWallet();

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

            this.federationWalletTransactionHandler = new FederationWalletTransactionHandler(this.loggerFactory, this.federationWalletManager, this.walletFeePolicy, this.network);

            var storeSettings = (StoreSettings)FormatterServices.GetUninitializedObject(typeof(StoreSettings));

            this.federationWalletSyncManager = new FederationWalletSyncManager(this.loggerFactory, this.federationWalletManager, chain, this.network,
                this.blockRepository, storeSettings, Substitute.For<INodeLifetime>());

            this.federationWalletSyncManager.Start();
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithChain()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.opReturnDataReader, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Test that after synchronizing with the chain the store tip equals the chain tip.
        /// </summary>
        [Fact]
        public void StartSynchronizesWithChainAndSurvivesRestart()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.opReturnDataReader, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);
            }

            // Create a new instance of this test that loads from the persistence that we created in the step before.
            var newTest = new CrossChainTransferStoreTests();
            ConcurrentChain newChain = newTest.BuildChain(3);

            using (var crossChainTransferStore2 = new CrossChainTransferStore(newTest.network, dataFolder, newChain, this.federationGatewaySettings, newTest.dateTimeProvider,
                newTest.loggerFactory, newTest.opReturnDataReader, newTest.fullNode, newTest.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore2.Initialize();

                // Test that the store was reloaded from persistence.
                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);

                // Test that synchronizing the store aligns it with the current chain tip.
                crossChainTransferStore2.Start();

                Assert.Equal(newChain.Tip.HashBlock, crossChainTransferStore2.TipHashAndHeight.Hash);
                Assert.Equal(newChain.Tip.Height, crossChainTransferStore2.TipHashAndHeight.Height);
            }
        }

        /// <summary>
        /// Recording a deposit creates a <see cref="CrossChainTransferStatus.Rejected" /> transfer if the balance is insufficient.
        /// </summary>
        [Fact]
        public void StoringDepositWhenWalletBalanceSufficientSucceeds()
        {
            ConcurrentChain chain = BuildChain(5);
            var dataFolder = new DataFolder(CreateTestDir(this));

            this.CreateWalletManagerAndTransactionHandler(chain, dataFolder);

            List<Block> blocks = this.blockRepository.GetBlocksAsync(chain.EnumerateAfter(this.network.GenesisHash).Select(h => h.HashBlock).ToList()).GetAwaiter().GetResult();

            foreach (Block block in blocks)
            {
                this.federationWalletSyncManager.ProcessBlock(block);
            }

            using (var crossChainTransferStore = new CrossChainTransferStore(this.network, dataFolder, chain, this.federationGatewaySettings, this.dateTimeProvider,
                this.loggerFactory, this.opReturnDataReader, this.fullNode, this.blockRepository, this.federationWalletManager, this.federationWalletTransactionHandler))
            {
                crossChainTransferStore.Initialize();
                crossChainTransferStore.Start();

                Assert.Equal(chain.Tip.HashBlock, crossChainTransferStore.TipHashAndHeight.Hash);
                Assert.Equal(chain.Tip.Height, crossChainTransferStore.TipHashAndHeight.Height);

                BitcoinAddress address = (new Key()).PubKey.Hash.GetAddress(this.network);

                Deposit deposit = new Deposit(0, 100, address.ToString(), crossChainTransferStore.NextMatureDepositHeight, 1);

                crossChainTransferStore.RecordLatestMatureDepositsAsync(new[] { deposit }).GetAwaiter().GetResult();

                // TODO: System.Exception: 'Transaction cannot be created by this consensus factory, please use the appropriate one.'
                // Transaction[] transaction = crossChainTransferStore.GetPartialTransactionsAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Builds a chain with the requested number of blocks.
        /// </summary>
        /// <param name="blocks">The number of blocks.</param>
        /// <returns>A chain with the requested number of blocks.</returns>
        private ConcurrentChain BuildChain(int blocks)
        {
            ConcurrentChain chain = new ConcurrentChain(this.network);

            this.blockDict.Clear();
            this.blockDict[this.network.GenesisHash] = this.network.GetGenesis();

            for (int i = 0; i < blocks - 1; i++)
            {
                this.AppendBlock(chain);
            }

            return chain;
        }

        /// <summary>
        /// Create a block and add it to the dictionary used by the mock block repository.
        /// </summary>
        /// <param name="previous">Previous chained header.</param>
        /// <param name="chains">Chains to add the block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(ChainedHeader previous, params ConcurrentChain[] chains)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ConcurrentChain chain in chains)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
                this.blockDict[block.GetHash()] = block;
            }
            return last;
        }

        /// <summary>
        /// Append a block to the specified chain(s).
        /// </summary>
        /// <param name="chains">The chains to append a block to.</param>
        /// <returns>The last chained header.</returns>
        private ChainedHeader AppendBlock(params ConcurrentChain[] chains)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chains);
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
