using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    /// <summary>
    /// Public interface for test chain context.
    /// </summary>
    public interface ITestChainContext
    {
        /// <summary>
        /// Memory pool validator interface;
        /// </summary>
        IMempoolValidator MempoolValidator { get; }

        /// <summary>
        /// List of the source transactions in the test chain.
        /// </summary>
        List<Transaction> SrcTxs { get; }
    }

    /// <summary>
    /// Concrete instance of the test chain.
    /// </summary>
    internal class TestChainContext : ITestChainContext
    {
        /// <inheritdoc />
        public IMempoolValidator MempoolValidator { get; set; }

        /// <inheritdoc />
        public List<Transaction> SrcTxs { get; set; }
    }

    /// <summary>
    /// Factory for creating the test chain.
    /// Much of this logic was taken directly from the embedded TestContext class in MinerTest.cs in the integration tests.
    /// </summary>
    internal class TestChainFactory
    {
        /// <summary>
        /// Creates the test chain with some default blocks and txs.
        /// </summary>
        /// <param name="network">Network to create the chain on.</param>
        /// <param name="scriptPubKey">Public key to create blocks/txs with.</param>
        /// <returns>Context object representing the test chain.</returns>
        public static async Task<ITestChainContext> CreateAsync(Network network, Script scriptPubKey, string dataDir)
        {
            var nodeSettings = new NodeSettings(network, args: new string[] { $"-datadir={dataDir}" });

            ILoggerFactory loggerFactory = nodeSettings.LoggerFactory;
            IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;

            network.Consensus.Options = new ConsensusOptions();
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(network.Consensus);

            // Dont check PoW of a header in this test.
            network.Consensus.HeaderValidationRules.RemoveAll(x => x.GetType() == typeof(CheckDifficultyPowRule));

            var consensusSettings = new ConsensusSettings(nodeSettings);
            var chain = new ConcurrentChain(network);
            InMemoryCoinView inMemoryCoinView = new InMemoryCoinView(chain.Tip.HashBlock);

            var cachedCoinView = new CachedCoinView(inMemoryCoinView, DateTimeProvider.Default, loggerFactory);
            var networkPeerFactory = new NetworkPeerFactory(network,
                dateTimeProvider,
                loggerFactory, new PayloadProvider().DiscoverPayloads(),
                new SelfEndpointTracker(loggerFactory),
                new Mock<IInitialBlockDownloadState>().Object,
                new ConnectionManagerSettings());

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, nodeSettings.DataFolder, loggerFactory, new SelfEndpointTracker(loggerFactory));
            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(loggerFactory), loggerFactory, network, networkPeerFactory, new NodeLifetime(), nodeSettings, peerAddressManager);
            var connectionSettings = new ConnectionManagerSettings(nodeSettings);
            var selfEndpointTracker = new SelfEndpointTracker(loggerFactory);
            var connectionManager = new ConnectionManager(dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeSettings,
                new NodeLifetime(), new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { },
                peerDiscovery, selfEndpointTracker, connectionSettings, new VersionProvider(), new Mock<INodeStats>().Object);

            var chainState = new ChainState();
            var peerBanning = new PeerBanning(connectionManager, loggerFactory, dateTimeProvider, peerAddressManager);
            var deployments = new NodeDeployments(network, chain);
            ConsensusRuleEngine consensusRules = new PowConsensusRuleEngine(network, loggerFactory, dateTimeProvider, chain, deployments, consensusSettings, new Checkpoints(),
                inMemoryCoinView, chainState, new InvalidBlockHashStore(new DateTimeProvider())).Register();

            var consensus = new ConsensusManager(network, loggerFactory, chainState, new HeaderValidator(consensusRules, loggerFactory),
                new IntegrityValidator(consensusRules, loggerFactory), new PartialValidator(consensusRules, loggerFactory), new FullValidator(consensusRules, loggerFactory), new Checkpoints(), consensusSettings, consensusRules,
                new Mock<IFinalizedBlockInfo>().Object, new Signals.Signals(), peerBanning, new Mock<IInitialBlockDownloadState>().Object, chain, new Mock<IBlockPuller>().Object,
                new Mock<IBlockStore>().Object, new InvalidBlockHashStore(new DateTimeProvider()), new Mock<IConnectionManager>().Object);

            var genesis = new ChainedHeader(network.GetGenesis().Header, network.GenesisHash, 0);
            chainState.BlockStoreTip = genesis;
            await consensus.InitializeAsync(genesis).ConfigureAwait(false);

            var blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(nodeSettings), loggerFactory, nodeSettings);
            var mempool = new TxMempool(dateTimeProvider, blockPolicyEstimator, loggerFactory, nodeSettings);
            var mempoolLock = new MempoolSchedulerLock();

            var minerSettings = new MinerSettings(nodeSettings);

            // Simple block creation, nothing special yet:
            PowBlockDefinition blockDefinition = new PowBlockDefinition(consensus, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network, consensusRules);
            BlockTemplate newBlock = blockDefinition.Build(chain.Tip, scriptPubKey);

            await consensus.BlockMinedAsync(newBlock.Block);

            List<BlockInfo> blockinfo = CreateBlockInfoList();

            // We can't make transactions until we have inputs
            // Therefore, load 100 blocks :)
            int baseheight = 0;
            var blocks = new List<Block>();
            var srcTxs = new List<Transaction>();
            for (int i = 0; i < blockinfo.Count; ++i)
            {
                Block currentBlock = Block.Load(newBlock.Block.ToBytes(network.Consensus.ConsensusFactory), network);
                currentBlock.Header.HashPrevBlock = chain.Tip.HashBlock;
                currentBlock.Header.Version = 1;
                currentBlock.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;

                Transaction txCoinbase = network.CreateTransaction(currentBlock.Transactions[0].ToBytes());
                txCoinbase.Inputs.Clear();
                txCoinbase.Version = 1;
                txCoinbase.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(blockinfo[i].extraNonce), Op.GetPushOp(chain.Height) })));
                // Ignore the (optional) segwit commitment added by CreateNewBlock (as the hardcoded nonces don't account for this)
                txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
                currentBlock.Transactions[0] = txCoinbase;

                if (srcTxs.Count == 0)
                    baseheight = chain.Height;
                if (srcTxs.Count < 4)
                    srcTxs.Add(currentBlock.Transactions[0]);

                currentBlock.UpdateMerkleRoot();

                currentBlock.Header.Nonce = blockinfo[i].nonce;

                chain.SetTip(currentBlock.Header);
                await consensus.BlockMinedAsync(currentBlock);
                blocks.Add(currentBlock);
            }

            // Just to make sure we can still make simple blocks
            blockDefinition = new PowBlockDefinition(consensus, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network, consensusRules);
            newBlock = blockDefinition.Build(chain.Tip, scriptPubKey);

            var mempoolValidator = new MempoolValidator(mempool, mempoolLock, dateTimeProvider, new MempoolSettings(nodeSettings), chain, cachedCoinView, loggerFactory, nodeSettings, consensusRules);

            return new TestChainContext { MempoolValidator = mempoolValidator, SrcTxs = srcTxs };
        }

        /// <summary>
        /// Info for a single block in the hard coded blocks that are loaded.
        /// </summary>
        private class BlockInfo
        {
            /// <summary>
            /// Extra nonce for the block.
            /// </summary>
            public int extraNonce;

            /// <summary>
            /// Nonce for the block.
            /// </summary>
            public uint nonce;
        }

        /// <summary>
        /// Source block information for hard coded blocks that are loaded.
        /// Pairs of extranonce, nonce fields.
        /// </summary>
        private static long[,] blockinfoarr =
        {
            {4, 0xa4a3e223}, {2, 0x15c32f9e}, {1, 0x0375b547}, {1, 0x7004a8a5},
            {2, 0xce440296}, {2, 0x52cfe198}, {1, 0x77a72cd0}, {2, 0xbb5d6f84},
            {2, 0x83f30c2c}, {1, 0x48a73d5b}, {1, 0xef7dcd01}, {2, 0x6809c6c4},
            {2, 0x0883ab3c}, {1, 0x087bbbe2}, {2, 0x2104a814}, {2, 0xdffb6daa},
            {1, 0xee8a0a08}, {2, 0xba4237c1}, {1, 0xa70349dc}, {1, 0x344722bb},
            {3, 0xd6294733}, {2, 0xec9f5c94}, {2, 0xca2fbc28}, {1, 0x6ba4f406},
            {2, 0x015d4532}, {1, 0x6e119b7c}, {2, 0x43e8f314}, {2, 0x27962f38},
            {2, 0xb571b51b}, {2, 0xb36bee23}, {2, 0xd17924a8}, {2, 0x6bc212d9},
            {1, 0x630d4948}, {2, 0x9a4c4ebb}, {2, 0x554be537}, {1, 0xd63ddfc7},
            {2, 0xa10acc11}, {1, 0x759a8363}, {2, 0xfb73090d}, {1, 0xe82c6a34},
            {1, 0xe33e92d7}, {3, 0x658ef5cb}, {2, 0xba32ff22}, {5, 0x0227a10c},
            {1, 0xa9a70155}, {5, 0xd096d809}, {1, 0x37176174}, {1, 0x830b8d0f},
            {1, 0xc6e3910e}, {2, 0x823f3ca8}, {1, 0x99850849}, {1, 0x7521fb81},
            {1, 0xaacaabab}, {1, 0xd645a2eb}, {5, 0x7aea1781}, {5, 0x9d6e4b78},
            {1, 0x4ce90fd8}, {1, 0xabdc832d}, {6, 0x4a34f32a}, {2, 0xf2524c1c},
            {2, 0x1bbeb08a}, {1, 0xad47f480}, {1, 0x9f026aeb}, {1, 0x15a95049},
            {2, 0xd1cb95b2}, {2, 0xf84bbda5}, {1, 0x0fa62cd1}, {1, 0xe05f9169},
            {1, 0x78d194a9}, {5, 0x3e38147b}, {5, 0x737ba0d4}, {1, 0x63378e10},
            {1, 0x6d5f91cf}, {2, 0x88612eb8}, {2, 0xe9639484}, {1, 0xb7fabc9d},
            {2, 0x19b01592}, {1, 0x5a90dd31}, {2, 0x5bd7e028}, {2, 0x94d00323},
            {1, 0xa9b9c01a}, {1, 0x3a40de61}, {1, 0x56e7eec7}, {5, 0x859f7ef6},
            {1, 0xfd8e5630}, {1, 0x2b0c9f7f}, {1, 0xba700e26}, {1, 0x7170a408},
            {1, 0x70de86a8}, {1, 0x74d64cd5}, {1, 0x49e738a1}, {2, 0x6910b602},
            {0, 0x643c565f}, {1, 0x54264b3f}, {2, 0x97ea6396}, {2, 0x55174459},
            {2, 0x03e8779a}, {1, 0x98f34d8f}, {1, 0xc07b2b07}, {1, 0xdfe29668},
            {1, 0x3141c7c1}, {1, 0xb3b595f4}, {1, 0x735abf08}, {5, 0x623bfbce},
            {2, 0xd351e722}, {1, 0xf4ca48c9}, {1, 0x5b19c670}, {1, 0xa164bf0e},
            {2, 0xbbbeb305}, {2, 0xfe1c810a}
        };

        /// <summary>
        /// Translates an array of extranonce, nonce pairs into list of block info.
        /// </summary>
        /// <returns>List of block info for hard coded blocks.</returns>
        private static List<BlockInfo> CreateBlockInfoList()
        {
            var blockInfoList = new List<BlockInfo>();
            List<long> lst = blockinfoarr.Cast<long>().ToList();
            for (int i = 0; i < lst.Count; i += 2)
                blockInfoList.Add(new BlockInfo { extraNonce = (int)lst[i], nonce = (uint)lst[i + 1] });
            return blockInfoList;
        }
    }
}