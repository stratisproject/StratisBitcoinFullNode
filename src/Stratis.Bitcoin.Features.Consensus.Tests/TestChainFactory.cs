using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Tests;
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

        public ConsensusLoop Consensus { get; set; }

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
        public static async Task<TestChainContext> CreateAsync(Network network, string dataDir)
        {
            var testChainContext = new TestChainContext() { Network = network };

            testChainContext.NodeSettings = new NodeSettings(network, args:new string[] { $"-datadir={dataDir}" });

            if (dataDir != null)
            {
                testChainContext.NodeSettings.DataDir = dataDir;
            }

            testChainContext.ConnectionSettings = new ConnectionManagerSettings();
            testChainContext.ConnectionSettings.Load(testChainContext.NodeSettings);

            testChainContext.LoggerFactory = testChainContext.NodeSettings.LoggerFactory;
            testChainContext.DateTimeProvider = DateTimeProvider.Default;
            network.Consensus.Options = new PowConsensusOptions();

            ConsensusSettings consensusSettings = new ConsensusSettings().Load(testChainContext.NodeSettings);
            testChainContext.Checkpoints = new Checkpoints();

            PowConsensusValidator consensusValidator = new PowConsensusValidator(network, testChainContext.Checkpoints, testChainContext.DateTimeProvider, testChainContext.LoggerFactory);
            testChainContext.Chain = new ConcurrentChain(network);
            CachedCoinView cachedCoinView = new CachedCoinView(new InMemoryCoinView(testChainContext.Chain.Tip.HashBlock), DateTimeProvider.Default, testChainContext.LoggerFactory);

            DataFolder dataFolder = new DataFolder(TestBase.AssureEmptyDir(dataDir));
            testChainContext.PeerAddressManager = new PeerAddressManager(DateTimeProvider.Default, dataFolder, testChainContext.LoggerFactory, new SelfEndpointTracker());

            testChainContext.MockConnectionManager = new Moq.Mock<IConnectionManager>();
            testChainContext.MockReadOnlyNodesCollection = new Moq.Mock<IReadOnlyNetworkPeerCollection>();
            testChainContext.MockConnectionManager.Setup(s => s.ConnectedPeers).Returns(testChainContext.MockReadOnlyNodesCollection.Object);
            testChainContext.MockConnectionManager.Setup(s => s.NodeSettings).Returns(testChainContext.NodeSettings);
            testChainContext.MockConnectionManager.Setup(s => s.ConnectionSettings).Returns(testChainContext.ConnectionSettings);

            testChainContext.ConnectionManager = testChainContext.MockConnectionManager.Object;

            LookaheadBlockPuller blockPuller = new LookaheadBlockPuller(testChainContext.Chain, testChainContext.ConnectionManager, testChainContext.LoggerFactory);
            testChainContext.PeerBanning = new PeerBanning(testChainContext.ConnectionManager, testChainContext.LoggerFactory, testChainContext.DateTimeProvider, testChainContext.PeerAddressManager);
            NodeDeployments deployments = new NodeDeployments(testChainContext.Network, testChainContext.Chain);
            ConsensusRules consensusRules = new PowConsensusRules(testChainContext.Network, testChainContext.LoggerFactory, testChainContext.DateTimeProvider, testChainContext.Chain, deployments, consensusSettings, testChainContext.Checkpoints).Register(new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration());
            testChainContext.Consensus = new ConsensusLoop(new AsyncLoopFactory(testChainContext.LoggerFactory), consensusValidator, new NodeLifetime(), testChainContext.Chain, cachedCoinView, blockPuller, new NodeDeployments(network, testChainContext.Chain), testChainContext.LoggerFactory, new ChainState(new InvalidBlockHashStore(testChainContext.DateTimeProvider)), testChainContext.ConnectionManager, testChainContext.DateTimeProvider, new Signals.Signals(), consensusSettings, testChainContext.NodeSettings, testChainContext.PeerBanning, consensusRules);
            await testChainContext.Consensus.StartAsync();

            return testChainContext;
        }

        /// <summary>
        /// Mine new blocks in to the consensus database and the chain.
        /// </summary>
        public static async Task<List<Block>> MineBlocksAsync(TestChainContext testChainContext, int count, Script scriptPubKey)
        {
            BlockPolicyEstimator blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(testChainContext.NodeSettings), testChainContext.LoggerFactory, testChainContext.NodeSettings);
            TxMempool mempool = new TxMempool(testChainContext.DateTimeProvider, blockPolicyEstimator, testChainContext.LoggerFactory, testChainContext.NodeSettings);
            MempoolSchedulerLock mempoolLock = new MempoolSchedulerLock();

            // Simple block creation, nothing special yet:

            List<Block> blocks = new List<Block>();
            for (int i = 0; i < count; ++i)
            {
                PowBlockAssembler blockAssembler = CreatePowBlockAssembler(testChainContext.Network, testChainContext.Consensus, testChainContext.Chain, mempoolLock, mempool, testChainContext.DateTimeProvider, testChainContext.LoggerFactory as LoggerFactory);

                BlockTemplate newBlock = blockAssembler.CreateNewBlock(scriptPubKey);

                int nHeight = testChainContext.Chain.Tip.Height + 1; // Height first in coinbase required for block.version=2
                Transaction txCoinbase = newBlock.Block.Transactions[0];
                txCoinbase.Inputs[0] = TxIn.CreateCoinbase(nHeight);
                newBlock.Block.UpdateMerkleRoot();

                var maxTries = int.MaxValue;

                while (maxTries > 0 && !newBlock.Block.CheckProofOfWork(testChainContext.Network.Consensus))
                {
                    ++newBlock.Block.Header.Nonce;
                    --maxTries;
                }

                if (maxTries == 0)
                    throw new XunitException("Test failed no blocks found");

                var context = new BlockValidationContext { Block = newBlock.Block };
                await testChainContext.Consensus.AcceptBlockAsync(context);
                Assert.Null(context.Error);

                blocks.Add(newBlock.Block);
            }

            return blocks;
        }

        /// <summary>
        /// Creates a proof of work block assembler.
        /// </summary>
        /// <param name="network">Network running on.</param>
        /// <param name="consensus">Consensus loop.</param>
        /// <param name="chain">Block chain.</param>
        /// <param name="mempoolLock">Async lock for memory pool.</param>
        /// <param name="mempool">Memory pool for transactions.</param>
        /// <param name="date">Date and time provider.</param>
        /// <returns>Proof of work block assembler.</returns>
        private static PowBlockAssembler CreatePowBlockAssembler(Network network, ConsensusLoop consensus, ConcurrentChain chain, MempoolSchedulerLock mempoolLock, TxMempool mempool, IDateTimeProvider date, LoggerFactory loggerFactory)
        {
            AssemblerOptions options = new AssemblerOptions();

            options.BlockMaxWeight = network.Consensus.Option<PowConsensusOptions>().MaxBlockWeight;
            options.BlockMaxSize = network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize;

            FeeRate blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);
            options.BlockMinFeeRate = blockMinFeeRate;

            return new PowBlockAssembler(consensus, network, mempoolLock, mempool, date, chain.Tip, loggerFactory, options);
        }
    }
}
