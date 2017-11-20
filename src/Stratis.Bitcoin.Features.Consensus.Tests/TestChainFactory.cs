using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;

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

        public LoggerFactory LoggerFactory { get; set; }

        public NodeSettings NodeSettings { get; set; }

        public ConcurrentChain Chain { get; set; }

        public Network Network { get; set; }
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
        public static async Task<TestChainContext> CreateAsync(Network network, Script scriptPubKey, string dataDir)
        {
            var testChainContext = new TestChainContext();

            testChainContext.NodeSettings = NodeSettings.FromArguments(new string[] { $"-datadir={dataDir}" }, network.Name, network);
            if (dataDir != null)
            {
                testChainContext.NodeSettings.DataDir = dataDir;
            }

            testChainContext.LoggerFactory = new LoggerFactory();
            testChainContext.DateTimeProvider = DateTimeProvider.Default;

            network.Consensus.Options = new PowConsensusOptions();
            ConsensusSettings consensusSettings = new ConsensusSettings(testChainContext.NodeSettings, testChainContext.LoggerFactory);
            PowConsensusValidator consensusValidator = new PowConsensusValidator(network, new Checkpoints(network, consensusSettings), testChainContext.DateTimeProvider, testChainContext.LoggerFactory);
            testChainContext.Chain = new ConcurrentChain(network);
            CachedCoinView cachedCoinView = new CachedCoinView(new InMemoryCoinView(testChainContext.Chain.Tip.HashBlock), DateTimeProvider.Default, testChainContext.LoggerFactory);

            ConnectionManager connectionManager = new ConnectionManager(network, new NodeConnectionParameters(), testChainContext.NodeSettings, testChainContext.LoggerFactory, new NodeLifetime());
            LookaheadBlockPuller blockPuller = new LookaheadBlockPuller(testChainContext.Chain, connectionManager, new LoggerFactory());
            testChainContext.PeerBanning = new PeerBanning(connectionManager, testChainContext.LoggerFactory, testChainContext.DateTimeProvider, testChainContext.NodeSettings);

            testChainContext.Consensus = new ConsensusLoop(new AsyncLoopFactory(testChainContext.LoggerFactory), consensusValidator, new NodeLifetime(), testChainContext.Chain, cachedCoinView, blockPuller, new NodeDeployments(network, testChainContext.Chain), testChainContext.LoggerFactory, new ChainState(new FullNode()), connectionManager, testChainContext.DateTimeProvider, new Signals.Signals(), new Checkpoints(network, consensusSettings), consensusSettings, testChainContext.PeerBanning);
            await testChainContext.Consensus.StartAsync();

        

            return testChainContext;
        }

        public static async Task MineBlocks(TestChainContext testChainContext, int count, Script scriptPubKey)
        {
            BlockPolicyEstimator blockPolicyEstimator = new BlockPolicyEstimator(new MempoolSettings(testChainContext.NodeSettings), testChainContext.LoggerFactory, testChainContext.NodeSettings);
            TxMempool mempool = new TxMempool(testChainContext.DateTimeProvider, blockPolicyEstimator, testChainContext.LoggerFactory, testChainContext.NodeSettings);
            MempoolSchedulerLock mempoolLock = new MempoolSchedulerLock();

            // Simple block creation, nothing special yet:
            PowBlockAssembler blockAssembler = CreatePowBlockAssembler(testChainContext.Network, testChainContext.Consensus, testChainContext.Chain, mempoolLock, mempool, testChainContext.DateTimeProvider, testChainContext.LoggerFactory);
            BlockTemplate newBlock = blockAssembler.CreateNewBlock(scriptPubKey);


            await testChainContext.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = newBlock.Block });

            int baseheight = 0;
            List<Block> blocks = new List<Block>();
            List<Transaction> srcTxs = new List<Transaction>();
            for (int i = 0; i < count; ++i)
            {
                Block currentBlock = newBlock.Block.Clone(); // pointer for convenience
                currentBlock.Header.HashPrevBlock = testChainContext.Chain.Tip.HashBlock;
                currentBlock.Header.Version = 1;
                currentBlock.Header.Time = Utils.DateTimeToUnixTime(testChainContext.Chain.Tip.GetMedianTimePast()) + 1;
                Transaction txCoinbase = currentBlock.Transactions[0].Clone();
                txCoinbase.Inputs.Clear();
                txCoinbase.Version = 1;
                var nonce = DateTime.UtcNow.Ticks + i;
                txCoinbase.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(nonce), Op.GetPushOp(testChainContext.Chain.Height) })));
                // Ignore the (optional) segwit commitment added by CreateNewBlock (as the hardcoded nonces don't account for this)
                txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
                currentBlock.Transactions[0] = txCoinbase;

                if (srcTxs.Count == 0)
                    baseheight = testChainContext.Chain.Height;
                if (srcTxs.Count < 4)
                    srcTxs.Add(currentBlock.Transactions[0]);
                currentBlock.UpdateMerkleRoot();
                currentBlock.Header.Nonce = (uint)nonce;
                await testChainContext.Consensus.AcceptBlockAsync(new BlockValidationContext { Block = currentBlock });
                blocks.Add(currentBlock);
            }
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

        ///// <summary>
        ///// Source block information for hard coded blocks that are loaded.
        ///// Pairs of extranonce, nonce fields.
        ///// </summary>
        //private static long[,] blockinfoarr =
        //{
        //    {4, 0xa4a3e223}, {2, 0x15c32f9e}, {1, 0x0375b547}, {1, 0x7004a8a5},
        //    {2, 0xce440296}, {2, 0x52cfe198}, {1, 0x77a72cd0}, {2, 0xbb5d6f84},
        //    {2, 0x83f30c2c}, {1, 0x48a73d5b}, {1, 0xef7dcd01}, {2, 0x6809c6c4},
        //    {2, 0x0883ab3c}, {1, 0x087bbbe2}, {2, 0x2104a814}, {2, 0xdffb6daa},
        //    {1, 0xee8a0a08}, {2, 0xba4237c1}, {1, 0xa70349dc}, {1, 0x344722bb},
        //    {3, 0xd6294733}, {2, 0xec9f5c94}, {2, 0xca2fbc28}, {1, 0x6ba4f406},
        //    {2, 0x015d4532}, {1, 0x6e119b7c}, {2, 0x43e8f314}, {2, 0x27962f38},
        //    {2, 0xb571b51b}, {2, 0xb36bee23}, {2, 0xd17924a8}, {2, 0x6bc212d9},
        //    {1, 0x630d4948}, {2, 0x9a4c4ebb}, {2, 0x554be537}, {1, 0xd63ddfc7},
        //    {2, 0xa10acc11}, {1, 0x759a8363}, {2, 0xfb73090d}, {1, 0xe82c6a34},
        //    {1, 0xe33e92d7}, {3, 0x658ef5cb}, {2, 0xba32ff22}, {5, 0x0227a10c},
        //    {1, 0xa9a70155}, {5, 0xd096d809}, {1, 0x37176174}, {1, 0x830b8d0f},
        //    {1, 0xc6e3910e}, {2, 0x823f3ca8}, {1, 0x99850849}, {1, 0x7521fb81},
        //    {1, 0xaacaabab}, {1, 0xd645a2eb}, {5, 0x7aea1781}, {5, 0x9d6e4b78},
        //    {1, 0x4ce90fd8}, {1, 0xabdc832d}, {6, 0x4a34f32a}, {2, 0xf2524c1c},
        //    {2, 0x1bbeb08a}, {1, 0xad47f480}, {1, 0x9f026aeb}, {1, 0x15a95049},
        //    {2, 0xd1cb95b2}, {2, 0xf84bbda5}, {1, 0x0fa62cd1}, {1, 0xe05f9169},
        //    {1, 0x78d194a9}, {5, 0x3e38147b}, {5, 0x737ba0d4}, {1, 0x63378e10},
        //    {1, 0x6d5f91cf}, {2, 0x88612eb8}, {2, 0xe9639484}, {1, 0xb7fabc9d},
        //    {2, 0x19b01592}, {1, 0x5a90dd31}, {2, 0x5bd7e028}, {2, 0x94d00323},
        //    {1, 0xa9b9c01a}, {1, 0x3a40de61}, {1, 0x56e7eec7}, {5, 0x859f7ef6},
        //    {1, 0xfd8e5630}, {1, 0x2b0c9f7f}, {1, 0xba700e26}, {1, 0x7170a408},
        //    {1, 0x70de86a8}, {1, 0x74d64cd5}, {1, 0x49e738a1}, {2, 0x6910b602},
        //    {0, 0x643c565f}, {1, 0x54264b3f}, {2, 0x97ea6396}, {2, 0x55174459},
        //    {2, 0x03e8779a}, {1, 0x98f34d8f}, {1, 0xc07b2b07}, {1, 0xdfe29668},
        //    {1, 0x3141c7c1}, {1, 0xb3b595f4}, {1, 0x735abf08}, {5, 0x623bfbce},
        //    {2, 0xd351e722}, {1, 0xf4ca48c9}, {1, 0x5b19c670}, {1, 0xa164bf0e},
        //    {2, 0xbbbeb305}, {2, 0xfe1c810a}
        //};

        /// <summary>
        /// Translates an array of extranonce, nonce pairs into list of block info.
        /// </summary>
        /// <returns>List of block info for hard coded blocks.</returns>
        //private static List<BlockInfo> CreateBlockInfoList()
        //{
        //    var blockInfoList = new List<BlockInfo>();
        //    List<long> lst = blockinfoarr.Cast<long>().ToList();
        //    for (int i = 0; i < lst.Count; i += 2)
        //        blockInfoList.Add(new BlockInfo { extraNonce = (int)lst[i], nonce = (uint)lst[i + 1] });
        //    return blockInfoList;
        //}

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
