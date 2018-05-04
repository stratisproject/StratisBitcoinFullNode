using System.Collections.Generic;
using System.Linq;
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
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
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
        /// <param name="receiver">Public key to create blocks/txs with.</param>
        /// <returns>Context object representing the test chain.</returns>
        public static async Task<ITestChainContext> CreateAsync(Network network, Script receiver, string dataDir)
        {
            NodeSettings nodeSettings = new NodeSettings(network, args: new string[] { $"-datadir={dataDir}" });

            var loggerFactory = nodeSettings.LoggerFactory;
            IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;

            network.Consensus.Options = new PowConsensusOptions();
            ConsensusSettings consensusSettings = new ConsensusSettings().Load(nodeSettings);
            PowConsensusValidator consensusValidator = new PowConsensusValidator(network, new Checkpoints(), dateTimeProvider, loggerFactory);
            ConcurrentChain chain = new ConcurrentChain(network);
            CachedCoinView cachedCoinView = new CachedCoinView(new InMemoryCoinView(chain.Tip.HashBlock), DateTimeProvider.Default, loggerFactory);
            NetworkPeerFactory networkPeerFactory = new NetworkPeerFactory(network, dateTimeProvider, loggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker());

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, nodeSettings.DataFolder, loggerFactory, new SelfEndpointTracker());
            var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(loggerFactory), loggerFactory, Network.Main, networkPeerFactory, new NodeLifetime(), nodeSettings, peerAddressManager);
            var connectionSettings = new ConnectionManagerSettings();
            connectionSettings.Load(nodeSettings);
            var connectionManager = new ConnectionManager(dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeSettings, new NodeLifetime(), new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { }, peerDiscovery, connectionSettings);

            LookaheadBlockPuller blockPuller = new LookaheadBlockPuller(chain, connectionManager, new LoggerFactory());
            PeerBanning peerBanning = new PeerBanning(connectionManager, loggerFactory, dateTimeProvider, peerAddressManager);
            NodeDeployments deployments = new NodeDeployments(network, chain);
            ConsensusRules consensusRules = new PowConsensusRules(network, loggerFactory, dateTimeProvider, chain, deployments, consensusSettings, new Checkpoints(), new InMemoryCoinView(new uint256()), new Mock<ILookaheadBlockPuller>().Object).Register(new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration());
            ConsensusLoop consensus = new ConsensusLoop(new AsyncLoopFactory(loggerFactory), consensusValidator, new NodeLifetime(), chain, cachedCoinView, blockPuller, deployments, loggerFactory, new ChainState(new InvalidBlockHashStore(dateTimeProvider)), connectionManager, dateTimeProvider, new Signals.Signals(), consensusSettings, nodeSettings, peerBanning, consensusRules);
            await consensus.StartAsync();

            BlockPolicyEstimator blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(nodeSettings), loggerFactory, nodeSettings);
            TxMempool mempool = new TxMempool(dateTimeProvider, blockPolicyEstimator, loggerFactory, nodeSettings);
            MempoolSchedulerLock mempoolLock = new MempoolSchedulerLock();

            // Simple block creation, nothing special yet:
            PowBlockAssembler blockAssembler = CreatePowBlockAssembler(consensus, dateTimeProvider, loggerFactory as LoggerFactory, mempool, mempoolLock, network);
            BlockTemplate newBlock = blockAssembler.Build(chain.Tip, receiver);
            chain.SetTip(newBlock.Block.Header);
            await consensus.ValidateAndExecuteBlockAsync(new RuleContext(new BlockValidationContext { Block = newBlock.Block }, network.Consensus, consensus.Tip) { CheckPow = false, CheckMerkleRoot = false });

            List<BlockInfo> blockinfo = CreateBlockInfoList();

            // We can't make transactions until we have inputs
            // Therefore, load 100 blocks :)
            int baseheight = 0;
            List<Block> blocks = new List<Block>();
            List<Transaction> srcTxs = new List<Transaction>();
            for (int i = 0; i < blockinfo.Count; ++i)
            {
                Block currentBlock = newBlock.Block.Clone(); // pointer for convenience
                currentBlock.Header.HashPrevBlock = chain.Tip.HashBlock;
                currentBlock.Header.Version = 1;
                currentBlock.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
                Transaction txCoinbase = currentBlock.Transactions[0].Clone();
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
                await consensus.ValidateAndExecuteBlockAsync(new RuleContext(new BlockValidationContext { Block = currentBlock }, network.Consensus, consensus.Tip) { CheckPow = false, CheckMerkleRoot = false });
                blocks.Add(currentBlock);
            }

            // Just to make sure we can still make simple blocks
            blockAssembler = CreatePowBlockAssembler(consensus, dateTimeProvider, loggerFactory as LoggerFactory, mempool, mempoolLock, network);
            newBlock = blockAssembler.Build(chain.Tip, receiver);

            MempoolValidator mempoolValidator = new MempoolValidator(mempool, mempoolLock, consensusValidator, dateTimeProvider, new MempoolSettings(nodeSettings), chain, cachedCoinView, loggerFactory, nodeSettings);

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

        /// <summary>
        /// Creates a proof of work block assembler.
        /// </summary>
        /// <param name="consensusLoop">Consensus loop.</param>
        /// <param name="dateTimeProvider">Date and time provider.</param>
        /// <param name="mempool">Memory pool for transactions.</param>
        /// <param name="mempoolLock">Async lock for memory pool.</param>
        /// <param name="network">Network running on.</param>
        /// <returns>Proof of work block assembler.</returns>
        private static PowBlockAssembler CreatePowBlockAssembler(IConsensusLoop consensusLoop, IDateTimeProvider dateTimeProvider, LoggerFactory loggerFactory, TxMempool mempool, MempoolSchedulerLock mempoolLock, Network network)
        {
            var options = new AssemblerOptions
            {
                BlockMaxWeight = network.Consensus.Option<PowConsensusOptions>().MaxBlockWeight,
                BlockMaxSize = network.Consensus.Option<PowConsensusOptions>().MaxBlockSerializedSize
            };

            var blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);
            options.BlockMinFeeRate = blockMinFeeRate;

            return new PowBlockAssembler(consensusLoop, dateTimeProvider, loggerFactory, mempool, mempoolLock, network, options);
        }
    }
}