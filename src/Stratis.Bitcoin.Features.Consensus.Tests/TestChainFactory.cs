using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestChainContext
    {
        public List<Block> Blocks { get; set; }

        public ConsensusManager Consensus { get; set; }

        public ConsensusRuleEngine ConsensusRules { get; set; }

        public PeerBanning PeerBanning { get; set; }

        public IDateTimeProvider DateTimeProvider { get; set; }

        public ILoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ConnectionManagerSettings ConnectionSettings { get; set; }

        public ConcurrentChain Chain { get; set; }

        public Network Network { get; set; }

        public IConnectionManager ConnectionManager { get; set; }

        public Mock<IConnectionManager> MockConnectionManager { get; set; }

        public Mock<IReadOnlyNetworkPeerCollection> MockReadOnlyNodesCollection { get; set; }

        public Checkpoints Checkpoints { get; set; }

        public IPeerAddressManager PeerAddressManager { get; set; }

        public IChainState ChainState { get; set; }

        public IFinalizedBlockInfoRepository FinalizedBlockInfo { get; set; }

        public IInitialBlockDownloadState InitialBlockDownloadState { get; set; }

        public HeaderValidator HeaderValidator { get; set; }

        public IntegrityValidator IntegrityValidator { get; set; }

        public PartialValidator PartialValidator { get; set; }

        public FullValidator FullValidator { get; set; }
    }

    /// <summary>
    /// Factory for creating the test chain.
    /// Much of this logic was taken directly from the embedded TestContext class in MinerTest.cs in the integration tests.
    /// </summary>
    internal class TestChainFactory
    {
        /// <summary>
        /// Creates test chain with a consensus loop.
        /// </summary>
        public static async Task<TestChainContext> CreateAsync(Network network, string dataDir, Mock<IPeerAddressManager> mockPeerAddressManager = null)
        {
            var testChainContext = new TestChainContext() { Network = network };

            testChainContext.NodeSettings = new NodeSettings(network, args: new string[] { $"-datadir={dataDir}" });
            testChainContext.ConnectionSettings = new ConnectionManagerSettings(testChainContext.NodeSettings);
            testChainContext.LoggerFactory = testChainContext.NodeSettings.LoggerFactory;
            testChainContext.DateTimeProvider = DateTimeProvider.Default;

            network.Consensus.Options = new ConsensusOptions();
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(network.Consensus);

            var consensusSettings = new ConsensusSettings(testChainContext.NodeSettings);
            testChainContext.Checkpoints = new Checkpoints();
            testChainContext.Chain = new ConcurrentChain(network);
            testChainContext.ChainState = new ChainState();
            testChainContext.InitialBlockDownloadState = new InitialBlockDownloadState(testChainContext.ChainState, testChainContext.Network, consensusSettings, new Checkpoints());

            var inMemoryCoinView = new InMemoryCoinView(testChainContext.Chain.Tip.HashBlock);
            var cachedCoinView = new CachedCoinView(inMemoryCoinView, DateTimeProvider.Default, testChainContext.LoggerFactory, new NodeStats(new DateTimeProvider()));

            var dataFolder = new DataFolder(TestBase.AssureEmptyDir(dataDir));
            testChainContext.PeerAddressManager =
                mockPeerAddressManager == null ?
                    new PeerAddressManager(DateTimeProvider.Default, dataFolder, testChainContext.LoggerFactory, new SelfEndpointTracker(testChainContext.LoggerFactory))
                    : mockPeerAddressManager.Object;

            testChainContext.MockConnectionManager = new Mock<IConnectionManager>();
            testChainContext.MockReadOnlyNodesCollection = new Mock<IReadOnlyNetworkPeerCollection>();
            testChainContext.MockConnectionManager.Setup(s => s.ConnectedPeers).Returns(testChainContext.MockReadOnlyNodesCollection.Object);
            testChainContext.MockConnectionManager.Setup(s => s.NodeSettings).Returns(testChainContext.NodeSettings);
            testChainContext.MockConnectionManager.Setup(s => s.ConnectionSettings).Returns(testChainContext.ConnectionSettings);

            testChainContext.ConnectionManager = testChainContext.MockConnectionManager.Object;
            var dateTimeProvider = new DateTimeProvider();

            testChainContext.PeerBanning = new PeerBanning(testChainContext.ConnectionManager, testChainContext.LoggerFactory, testChainContext.DateTimeProvider, testChainContext.PeerAddressManager);
            var deployments = new NodeDeployments(testChainContext.Network, testChainContext.Chain);
            testChainContext.ConsensusRules = new PowConsensusRuleEngine(testChainContext.Network, testChainContext.LoggerFactory, testChainContext.DateTimeProvider,
                testChainContext.Chain, deployments, consensusSettings, testChainContext.Checkpoints, cachedCoinView, testChainContext.ChainState,
                    new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider)).Register();

            testChainContext.HeaderValidator = new HeaderValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.IntegrityValidator = new IntegrityValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.PartialValidator = new PartialValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);
            testChainContext.FullValidator = new FullValidator(testChainContext.ConsensusRules, testChainContext.LoggerFactory);

            var dBreezeSerializer = new DBreezeSerializer(network);

            var blockRepository = new BlockRepository(testChainContext.Network, dataFolder, testChainContext.LoggerFactory, dBreezeSerializer);

            var blockStoreFlushCondition = new BlockStoreQueueFlushCondition(testChainContext.ChainState, testChainContext.InitialBlockDownloadState);

            var blockStore = new BlockStoreQueue(testChainContext.Chain, testChainContext.ChainState, blockStoreFlushCondition, new Mock<StoreSettings>().Object,
                blockRepository, testChainContext.LoggerFactory, new Mock<INodeStats>().Object);

            await blockStore.InitializeAsync();

            testChainContext.Consensus = ConsensusManagerHelper.CreateConsensusManager(network, dataDir);

            await testChainContext.Consensus.InitializeAsync(testChainContext.Chain.Tip);

            return testChainContext;
        }

        public static async Task<List<Block>> MineBlocksWithLastBlockMutatedAsync(TestChainContext testChainContext,
            int count, Script receiver)
        {
            return await MineBlocksAsync(testChainContext, count, receiver, true);
        }

        public static async Task<List<Block>> MineBlocksAsync(TestChainContext testChainContext,
            int count, Script receiver)
        {
            return await MineBlocksAsync(testChainContext, count, receiver, false);
        }

        /// <summary>
        /// Mine new blocks in to the consensus database and the chain.
        /// </summary>
        private static async Task<List<Block>> MineBlocksAsync(TestChainContext testChainContext, int count, Script receiver, bool mutateLastBlock)
        {
            var blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(testChainContext.NodeSettings), testChainContext.LoggerFactory, testChainContext.NodeSettings);
            var mempool = new TxMempool(testChainContext.DateTimeProvider, blockPolicyEstimator, testChainContext.LoggerFactory, testChainContext.NodeSettings);
            var mempoolLock = new MempoolSchedulerLock();

            // Simple block creation, nothing special yet:
            var blocks = new List<Block>();
            for (int i = 0; i < count; i++)
            {
                BlockTemplate newBlock = await MineBlockAsync(testChainContext, receiver, mempool, mempoolLock, mutateLastBlock && i == count - 1);

                blocks.Add(newBlock.Block);
            }

            return blocks;
        }

        private static async Task<BlockTemplate> MineBlockAsync(TestChainContext testChainContext, Script scriptPubKey, TxMempool mempool,
            MempoolSchedulerLock mempoolLock, bool getMutatedBlock = false)
        {
            BlockTemplate newBlock = CreateBlockTemplate(testChainContext, scriptPubKey, mempool, mempoolLock);

            if (getMutatedBlock) BuildMutatedBlock(newBlock);

            newBlock.Block.UpdateMerkleRoot();

            TryFindNonceForProofOfWork(testChainContext, newBlock);

            if (!getMutatedBlock) await ValidateBlock(testChainContext, newBlock);
            else CheckBlockIsMutated(newBlock);

            return newBlock;
        }

        private static BlockTemplate CreateBlockTemplate(TestChainContext testChainContext, Script scriptPubKey,
            TxMempool mempool, MempoolSchedulerLock mempoolLock)
        {
            PowBlockDefinition blockAssembler = new PowBlockDefinition(testChainContext.Consensus,
                testChainContext.DateTimeProvider, testChainContext.LoggerFactory as LoggerFactory, mempool, mempoolLock,
                new MinerSettings(testChainContext.NodeSettings), testChainContext.Network, testChainContext.ConsensusRules);

            BlockTemplate newBlock = blockAssembler.Build(testChainContext.Chain.Tip, scriptPubKey);

            int nHeight = testChainContext.Chain.Tip.Height + 1; // Height first in coinbase required for block.version=2
            Transaction txCoinbase = newBlock.Block.Transactions[0];
            txCoinbase.Inputs[0] = TxIn.CreateCoinbase(nHeight);
            return newBlock;
        }

        private static void BuildMutatedBlock(BlockTemplate newBlock)
        {
            Transaction coinbaseTransaction = newBlock.Block.Transactions[0];
            Transaction outTransaction = TransactionsHelper.BuildNewTransactionFromExistingTransaction(coinbaseTransaction, 0);
            newBlock.Block.Transactions.Add(outTransaction);
            Transaction duplicateTransaction = TransactionsHelper.BuildNewTransactionFromExistingTransaction(coinbaseTransaction, 1);
            newBlock.Block.Transactions.Add(duplicateTransaction);
            newBlock.Block.Transactions.Add(duplicateTransaction);
        }

        private static void TryFindNonceForProofOfWork(TestChainContext testChainContext, BlockTemplate newBlock)
        {
            int maxTries = int.MaxValue;
            while (maxTries > 0 && !newBlock.Block.CheckProofOfWork())
            {
                ++newBlock.Block.Header.Nonce;
                --maxTries;
            }

            if (maxTries == 0)
                throw new XunitException("Test failed no blocks found");
        }

        private static void CheckBlockIsMutated(BlockTemplate newBlock)
        {
            List<uint256> transactionHashes = newBlock.Block.Transactions.Select(t => t.GetHash()).ToList();
            BlockMerkleRootRule.ComputeMerkleRoot(transactionHashes, out bool isMutated);
            isMutated.Should().Be(true);
        }

        private static async Task ValidateBlock(TestChainContext testChainContext, BlockTemplate newBlock)
        {
            var res = await testChainContext.Consensus.BlockMinedAsync(newBlock.Block);
            Assert.NotNull(res);
        }

    }
}