using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class MinerTests
    {
        private const string Password = "password";
        private const string WalletName = "mywallet";
        private const string Passphrase = "passphrase";
        private const string Account = "account 0";

        private readonly Network network;

        private static FeeRate blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

        public static PowBlockDefinition AssemblerForTest(TestContext testContext)
        {
            return new PowBlockDefinition(testContext.consensus, testContext.DateTimeProvider, new LoggerFactory(), testContext.mempool, testContext.mempoolLock, new MinerSettings(NodeSettings.Default(testContext.network)), testContext.network, testContext.ConsensusRules);
        }

        public class Blockinfo
        {
            public int extranonce;
            public uint nonce;
        }

        public static long[,] blockinfoarr =
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

        public bool TestSequenceLocks(TestContext testContext, ChainedHeader chainedHeader, Transaction tx, Transaction.LockTimeFlags flags, LockPoints uselock = null)
        {
            var context = new MempoolValidationContext(tx, new MempoolValidationState(false));
            context.View = new MempoolCoinView(testContext.cachedCoinView, testContext.mempool, testContext.mempoolLock, null);
            context.View.LoadViewAsync(tx).GetAwaiter().GetResult();
            return MempoolValidator.CheckSequenceLocks(testContext.network, chainedHeader, context, flags, uselock, false);
        }

        // TODO: There may be an opportunity to share the logic for populating the chain (TestContext) using TestChainFactory in the mempool unit tests.
        //       Most of the logic for mempool's TestChainFactory was taken directly from the "TestContext" class that is embedded below.
        public class TestContext
        {
            public List<Blockinfo> blockinfo;
            private uint nonce;
            public Network network;
            public Script scriptPubKey;
            public BlockTemplate newBlock;
            public Transaction tx, tx2;
            public Script script;
            public uint256 hash;
            public TestMemPoolEntryHelper entry;
            public ConcurrentChain chain;
            public ConsensusManager consensus;
            public ConsensusRuleEngine ConsensusRules;
            public DateTimeProvider DateTimeProvider;
            public TxMempool mempool;
            public MempoolSchedulerLock mempoolLock;
            public List<Transaction> txFirst;
            public Money BLOCKSUBSIDY = 50 * Money.COIN;
            public Money LOWFEE = Money.CENT;
            public Money HIGHFEE = Money.COIN;
            public Money HIGHERFEE = 4 * Money.COIN;
            public int baseheight;
            public CachedCoinView cachedCoinView;

            public async Task InitializeAsync()
            {
                this.blockinfo = new List<Blockinfo>();
                List<long> lst = blockinfoarr.Cast<long>().ToList();
                for (int i = 0; i < lst.Count; i += 2)
                    this.blockinfo.Add(new Blockinfo { extranonce = (int)lst[i], nonce = (uint)lst[i + 1] });

                // Note that by default, these tests run with size accounting enabled.
                this.network = KnownNetworks.RegTest;
                byte[] hex = Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f");
                this.scriptPubKey = new Script(new[] { Op.GetPushOp(hex), OpcodeType.OP_CHECKSIG });

                this.entry = new TestMemPoolEntryHelper();
                this.chain = new ConcurrentChain(this.network);
                this.network.Consensus.Options = new ConsensusOptions();

                IDateTimeProvider dateTimeProvider = DateTimeProvider.Default;

                var inMemoryCoinView = new InMemoryCoinView(this.chain.Tip.HashBlock);
                this.cachedCoinView = new CachedCoinView(inMemoryCoinView, dateTimeProvider, new LoggerFactory(), new NodeStats(dateTimeProvider));

                var loggerFactory = new ExtendedLoggerFactory();
                loggerFactory.AddConsoleWithFilters();

                var nodeSettings = new NodeSettings(this.network, args: new string[] { "-checkpoints" });
                var consensusSettings = new ConsensusSettings(nodeSettings);

                var networkPeerFactory = new NetworkPeerFactory(this.network, dateTimeProvider, loggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker(loggerFactory), new Mock<IInitialBlockDownloadState>().Object, new ConnectionManagerSettings(nodeSettings));

                var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, nodeSettings.DataFolder, loggerFactory, new SelfEndpointTracker(loggerFactory));
                var peerDiscovery = new PeerDiscovery(new AsyncLoopFactory(loggerFactory), loggerFactory, this.network, networkPeerFactory, new NodeLifetime(), nodeSettings, peerAddressManager);
                var connectionSettings = new ConnectionManagerSettings(nodeSettings);
                var selfEndpointTracker = new SelfEndpointTracker(loggerFactory);
                var connectionManager = new ConnectionManager(dateTimeProvider, loggerFactory, this.network, networkPeerFactory,
                    nodeSettings, new NodeLifetime(), new NetworkPeerConnectionParameters(), peerAddressManager, new IPeerConnector[] { },
                    peerDiscovery, selfEndpointTracker, connectionSettings, new VersionProvider(), new Mock<INodeStats>().Object);

                var peerBanning = new PeerBanning(connectionManager, loggerFactory, dateTimeProvider, peerAddressManager);
                var deployments = new NodeDeployments(this.network, this.chain);

                var genesis = this.network.GetGenesis();

                var chainState = new ChainState()
                {
                    BlockStoreTip = new ChainedHeader(genesis.Header, genesis.GetHash(), 0)
                };

                this.ConsensusRules = new PowConsensusRuleEngine(this.network, loggerFactory, dateTimeProvider, this.chain, deployments, consensusSettings,
                    new Checkpoints(), this.cachedCoinView, chainState, new InvalidBlockHashStore(dateTimeProvider), new NodeStats(dateTimeProvider)).Register();

                this.consensus = ConsensusManagerHelper.CreateConsensusManager(this.network, chainState: chainState, inMemoryCoinView: inMemoryCoinView, chain: this.chain);

                await this.consensus.InitializeAsync(chainState.BlockStoreTip);

                this.entry.Fee(11);
                this.entry.Height(11);

                var dateTimeProviderSet = new DateTimeProviderSet
                {
                    time = dateTimeProvider.GetTime(),
                    timeutc = dateTimeProvider.GetUtcNow()
                };

                this.DateTimeProvider = dateTimeProviderSet;
                this.mempool = new TxMempool(dateTimeProvider, new BlockPolicyEstimator(new MempoolSettings(nodeSettings), loggerFactory, nodeSettings), loggerFactory, nodeSettings);
                this.mempoolLock = new MempoolSchedulerLock();

                // We can't make transactions until we have inputs
                // Therefore, load 100 blocks :)
                this.baseheight = 0;
                var blocks = new List<Block>();
                this.txFirst = new List<Transaction>();

                this.nonce = 0;

                for (int i = 0; i < this.blockinfo.Count; ++i)
                {
                    Block block = this.network.CreateBlock();
                    block.Header.HashPrevBlock = this.consensus.Tip.HashBlock;
                    block.Header.Version = 1;
                    block.Header.Time = Utils.DateTimeToUnixTime(this.chain.Tip.GetMedianTimePast()) + 1;

                    Transaction txCoinbase = this.network.CreateTransaction();
                    txCoinbase.Version = 1;
                    txCoinbase.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(this.blockinfo[i].extranonce), Op.GetPushOp(this.chain.Height) })));
                    // Ignore the (optional) segwit commitment added by CreateNewBlock (as the hardcoded nonces don't account for this)
                    txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
                    block.AddTransaction(txCoinbase);

                    if (this.txFirst.Count == 0)
                        this.baseheight = this.chain.Height;

                    if (this.txFirst.Count < 4)
                        this.txFirst.Add(block.Transactions[0]);

                    block.Header.Bits = block.Header.GetWorkRequired(this.network, this.chain.Tip);

                    block.UpdateMerkleRoot();

                    while (!block.CheckProofOfWork())
                        block.Header.Nonce = ++this.nonce;

                    // Serialization sets the BlockSize property.
                    block = Block.Load(block.ToBytes(), this.network);

                    var res = await this.consensus.BlockMinedAsync(block);

                    if (res == null)
                        throw new InvalidOperationException();

                    blocks.Add(block);
                }

                // Just to make sure we can still make simple blocks
                this.newBlock = AssemblerForTest(this).Build(this.chain.Tip, this.scriptPubKey);
                Assert.NotNull(this.newBlock);
            }
        }

        public MinerTests()
        {
            this.network = new BitcoinRegTest();
        }

        // Test suite for ancestor feerate transaction selection.
        // Implemented as an additional function, rather than a separate test case,
        // to allow reusing the blockchain created in CreateNewBlock_validity.
        [Fact]
        public async Task MinerTestPackageSelectionAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();

            // Test the ancestor feerate transaction selection.
            var entry = new TestMemPoolEntryHelper();

            // Test that a medium fee transaction will be selected after a higher fee
            // rate package with a low fee rate parent.
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn(new OutPoint(context.txFirst[0].GetHash(), 0), new Script(OpcodeType.OP_1)));
            tx.AddOutput(new TxOut(new Money(5000000000L - 1000), new Script()));

            // This tx has a low fee: 1000 satoshis
            uint256 hashParentTx = tx.GetHash(); // save this txid for later use
            context.mempool.AddUnchecked(hashParentTx, entry.Fee(1000).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(true).FromTx(tx));

            // This tx has a medium fee: 10000 satoshis
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
            tx.Outputs[0].Value = 5000000000L - 10000;
            uint256 hashMediumFeeTx = tx.GetHash();
            context.mempool.AddUnchecked(hashMediumFeeTx, entry.Fee(10000).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(true).FromTx(tx));

            // This tx has a high fee, but depends on the first transaction
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = hashParentTx;
            tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 50k satoshi fee
            uint256 hashHighFeeTx = tx.GetHash();
            context.mempool.AddUnchecked(hashHighFeeTx, entry.Fee(50000).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(false).FromTx(tx));

            BlockTemplate pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            Assert.True(pblocktemplate.Block.Transactions[1].GetHash() == hashParentTx);
            Assert.True(pblocktemplate.Block.Transactions[2].GetHash() == hashHighFeeTx);
            Assert.True(pblocktemplate.Block.Transactions[3].GetHash() == hashMediumFeeTx);

            // Test that a package below the block min tx fee doesn't get included
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = hashHighFeeTx;
            tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 0 fee
            uint256 hashFreeTx = tx.GetHash();
            context.mempool.AddUnchecked(hashFreeTx, entry.Fee(0).FromTx(tx));
            int freeTxSize = tx.GetSerializedSize();

            // Calculate a fee on child transaction that will put the package just
            // below the block min tx fee (assuming 1 child tx of the same size).
            Money feeToUse = blockMinFeeRate.GetFee(2 * freeTxSize) - 1;

            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = hashFreeTx;
            tx.Outputs[0].Value = 5000000000L - 1000 - 50000 - feeToUse;
            uint256 hashLowFeeTx = tx.GetHash();
            context.mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse).FromTx(tx));
            pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            // Verify that the free tx and the low fee tx didn't get selected
            for (int i = 0; i < pblocktemplate.Block.Transactions.Count; ++i)
            {
                Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashFreeTx);
                Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashLowFeeTx);
            }

            // Test that packages above the min relay fee do get included, even if one
            // of the transactions is below the min relay fee
            // Remove the low fee transaction and replace with a higher fee transaction
            context.mempool.RemoveRecursive(tx);
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Outputs[0].Value -= 2; // Now we should be just over the min relay fee
            hashLowFeeTx = tx.GetHash();
            context.mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse + 2).FromTx(tx));
            pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            Assert.True(pblocktemplate.Block.Transactions[4].GetHash() == hashFreeTx);
            Assert.True(pblocktemplate.Block.Transactions[5].GetHash() == hashLowFeeTx);

            // Test that transaction selection properly updates ancestor fee
            // calculations as ancestor transactions get included in a block.
            // Add a 0-fee transaction that has 2 outputs.
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = context.txFirst[2].GetHash();
            tx.AddOutput(Money.Zero, new Script());
            tx.Outputs[0].Value = 5000000000L - 100000000;
            tx.Outputs[1].Value = 100000000; // 1BTC output
            uint256 hashFreeTx2 = tx.GetHash();
            context.mempool.AddUnchecked(hashFreeTx2, entry.Fee(0).SpendsCoinbase(true).FromTx(tx));

            // This tx can't be mined by itself
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = hashFreeTx2;
            tx.Outputs.RemoveAt(1);
            feeToUse = blockMinFeeRate.GetFee(freeTxSize);
            tx.Outputs[0].Value = 5000000000L - 100000000 - feeToUse;
            uint256 hashLowFeeTx2 = tx.GetHash();
            context.mempool.AddUnchecked(hashLowFeeTx2, entry.Fee(feeToUse).SpendsCoinbase(false).FromTx(tx));
            pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);

            // Verify that this tx isn't selected.
            for (int i = 0; i < pblocktemplate.Block.Transactions.Count; ++i)
            {
                Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashFreeTx2);
                Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashLowFeeTx2);
            }

            // This tx will be mineable, and should cause hashLowFeeTx2 to be selected
            // as well.
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.N = 1;
            tx.Outputs[0].Value = 100000000 - 10000; // 10k satoshi fee
            context.mempool.AddUnchecked(tx.GetHash(), entry.Fee(10000).FromTx(tx));
            pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            Assert.True(pblocktemplate.Block.Transactions[8].GetHash() == hashLowFeeTx2);
        }

        [Fact]
        public void MinerCreateBlockSigopsLimit1000()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(this.network).WithDummyWallet().Start();

                TestHelper.MineBlocks(miner, 1);

                var txMempoolHelper = new TestMemPoolEntryHelper();

                // Block sigops > limit: 1000 CHECKMULTISIG + 1
                var genesis = this.network.GetGenesis();
                var genesisCoinbase = genesis.Transactions[0];
                var tx = this.network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(genesisCoinbase.GetHash(), 0), new Script(new byte[] { (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_NOP, (byte)OpcodeType.OP_CHECKMULTISIG, (byte)OpcodeType.OP_1 })));

                // NOTE: OP_NOP is used to force 20 SigOps for the CHECKMULTISIG
                tx.AddOutput(Money.Coins(50), new Script());
                for (int i = 0; i < 1001; ++i)
                {
                    tx.Outputs[0].Value -= Money.CENT;
                    bool spendsCoinbase = (i == 0); // only first tx spends coinbase
                                                    // If we don't set the # of sig ops in the CTxMemPoolEntry, template creation fails
                    var txMempoolEntry = txMempoolHelper.Fee(Money.CENT).Time(DateTimeProvider.Default.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx);
                    miner.FullNode.NodeService<ITxMempool>().AddUnchecked(tx.GetHash(), txMempoolEntry);

                    tx = this.network.CreateTransaction(tx.ToBytes());
                    tx.Inputs[0].PrevOut.Hash = tx.GetHash();
                }

                var error = Assert.Throws<ConsensusException>(() => TestHelper.MineBlocks(miner, 1));
                Assert.True(error.Message == ConsensusErrors.BadBlockSigOps.ToString());

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

                Assert.True(miner.FullNode.ConsensusManager().Tip.Height == 1);
            }
        }

        [Fact]
        public async Task MinerCreateBlockSizeGreaterThenLimitAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());

            // block size > limit
            tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());
            tx.Inputs[0].ScriptSig = new Script();
            // 18 * (520char + DROP) + OP_1 = 9433 bytes
            var vchData = new byte[520];
            for (int i = 0; i < 18; ++i)
                tx.Inputs[0].ScriptSig = new Script(tx.Inputs[0].ScriptSig.ToBytes().Concat(vchData.Concat(new[] { (byte)OpcodeType.OP_DROP })));
            tx.Inputs[0].ScriptSig = new Script(tx.Inputs[0].ScriptSig.ToBytes().Concat(new[] { (byte)OpcodeType.OP_1 }));
            tx.Inputs[0].PrevOut.Hash = context.txFirst[0].GetHash();
            tx.Outputs[0].Value = context.BLOCKSUBSIDY;
            for (int i = 0; i < 128; ++i)
            {
                tx.Outputs[0].Value -= context.LOWFEE;
                context.hash = tx.GetHash();
                bool spendsCoinbase = (i == 0); // only first tx spends coinbase
                context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.LOWFEE).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
                tx = context.network.CreateTransaction(tx.ToBytes());
                tx.Inputs[0].PrevOut.Hash = context.hash;
            }
            BlockTemplate pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            Assert.NotNull(pblocktemplate);
            context.mempool.Clear();
        }

        [Fact]
        public async Task MinerCreateBlockChildWithHigherFeerateThanParentAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());

            // child with higher feerate than parent
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
            tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHFEE).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(true).FromTx(tx));
            tx = context.network.CreateTransaction(tx.ToBytes());
            tx.Inputs[0].PrevOut.Hash = context.hash;
            tx.Inputs.Add(new TxIn());
            tx.Inputs[1].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[1].PrevOut.Hash = context.txFirst[0].GetHash();
            tx.Inputs[1].PrevOut.N = 0;
            tx.Outputs[0].Value = tx.Outputs[0].Value + context.BLOCKSUBSIDY - context.HIGHERFEE; //First txn output + fresh coinbase - new txn fee
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHERFEE).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(true).FromTx(tx));
            BlockTemplate pblocktemplate = AssemblerForTest(context).Build(context.chain.Tip, context.scriptPubKey);
            Assert.NotNull(pblocktemplate);
            context.mempool.Clear();
        }

        [Fact]
        public void MinerCreateBlockCoinbaseMempoolTemplateCreationFails()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(this.network).WithDummyWallet().Start();

                TestHelper.MineBlocks(miner, 1);

                // Create an invalid coinbase transaction to be added to the mempool.
                var duplicateCoinbase = this.network.CreateTransaction();
                duplicateCoinbase.AddInput(new TxIn());
                duplicateCoinbase.AddOutput(new TxOut());
                duplicateCoinbase.Inputs[0].PrevOut = new OutPoint();
                duplicateCoinbase.Inputs[0].ScriptSig = new Script(OpcodeType.OP_0, OpcodeType.OP_1);
                duplicateCoinbase.Outputs[0].Value = 0;

                var txMempoolHelper = new TestMemPoolEntryHelper();
                var txMempoolEntry = txMempoolHelper.Fee(Money.CENT).Time(DateTimeProvider.Default.GetTime()).SpendsCoinbase(false).FromTx(duplicateCoinbase);
                miner.FullNode.NodeService<ITxMempool>().AddUnchecked(duplicateCoinbase.GetHash(), txMempoolEntry);

                var error = Assert.Throws<ConsensusException>(() => TestHelper.MineBlocks(miner, 1));
                Assert.True(error.Message == ConsensusErrors.BadMultipleCoinbase.ToString());

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(miner));

                Assert.True(miner.FullNode.ConsensusManager().Tip.Height == 1);
            }
        }

        [Fact]
        public async Task MinerCreateBlockNonFinalTxsInMempoolAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());

            // non - final txs in mempool
            (context.DateTimeProvider as DateTimeProviderSet).time = context.chain.Tip.Header.Time + 1;
            //SetMockTime(chainActive.Tip().GetMedianTimePast() + 1);
            Transaction.LockTimeFlags flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
            // height map
            var prevheights = new List<int>();

            // relative height locked
            tx.Version = 2;
            prevheights.Add(1);
            tx.Inputs[0].PrevOut.Hash = context.txFirst[0].GetHash(); // only 1 transaction
            tx.Inputs[0].PrevOut.N = 0;
            tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[0].Sequence = new Sequence(context.chain.Tip.Height + 1); // txFirst[0] is the 2nd block
            prevheights[0] = context.baseheight + 1;
            tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
            tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
            tx.LockTime = 0;
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHFEE).Time(context.DateTimeProvider.GetTime()).SpendsCoinbase(true).FromTx(tx));
            Assert.True(MempoolValidator.CheckFinalTransaction(context.chain, context.DateTimeProvider, tx, flags)); // Locktime passes
            Assert.True(!this.TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks fail

            BlockHeader blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1;
            context.chain.SetTip(blockHeader);

            blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1;
            context.chain.SetTip(blockHeader);

            blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1;
            context.chain.SetTip(blockHeader);

            SequenceLock locks = tx.CalculateSequenceLocks(prevheights.ToArray(), context.chain.Tip, flags);
            Assert.True(locks.Evaluate(context.chain.Tip)); // Sequence locks pass on 2nd block
        }

        [Fact]
        public async Task MinerCreateBlockRelativeTimeLockedAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());

            Transaction.LockTimeFlags flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

            // height map
            var prevheights = new List<int>();
            prevheights.Add(1);
            // relative time locked
            tx.Version = 2;
            tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
            tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[0].PrevOut.N = 0;
            tx.Inputs[0].Sequence = new Sequence(TimeSpan.FromMinutes(10)); // txFirst[1] is the 3rd block
            tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
            tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
            tx.LockTime = 0;
            prevheights[0] = context.baseheight + 2;
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Time(context.DateTimeProvider.GetTime()).FromTx(tx));
            Assert.True(MempoolValidator.CheckFinalTransaction(context.chain, context.DateTimeProvider, tx, flags)); // Locktime passes
            Assert.True(!this.TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks fail
        }

        [Fact]
        public async Task MinerCreateBlockAbsoluteHeightLockedAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());
            Transaction.LockTimeFlags flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

            int MedianTimeSpan = 11;
            var prevheights = new List<int>();
            prevheights.Add(1);
            tx.Version = 2;
            tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
            tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[0].PrevOut.N = 0;
            tx.Inputs[0].Sequence = new Sequence(TimeSpan.FromMinutes(10)); // txFirst[1] is the 3rd block
            tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
            tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
            tx.LockTime = 0;
            prevheights[0] = context.baseheight + 2;

            for (int i = 0; i < MedianTimeSpan; i++)
            {
                BlockHeader header = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.HashPrevBlock = context.chain.Tip.HashBlock;
                header.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512;
                context.chain.SetTip(header);
            }

            SequenceLock locks = (tx.CalculateSequenceLocks(prevheights.ToArray(), context.chain.Tip, flags));
            Assert.True(locks.Evaluate(context.chain.Tip));

            context = new TestContext();
            await context.InitializeAsync();

            // absolute height locked
            tx.Inputs[0].PrevOut.Hash = context.txFirst[2].GetHash();
            tx.Inputs[0].Sequence = Sequence.Final - 1;
            prevheights[0] = context.baseheight + 3;
            tx.LockTime = context.chain.Tip.Height + 1;
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Time(context.DateTimeProvider.GetTime()).FromTx(tx));
            Assert.True(!MempoolValidator.CheckFinalTransaction(context.chain, context.DateTimeProvider, tx, flags)); // Locktime fails
            Assert.True(this.TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks pass

            BlockHeader blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512;
            context.chain.SetTip(blockHeader);

            blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512;
            context.chain.SetTip(blockHeader);

            Assert.True(tx.IsFinal(context.chain.Tip.GetMedianTimePast(), context.chain.Tip.Height + 2)); // Locktime passes on 2nd block
        }

        [Fact]
        public async Task MinerCreateBlockAbsoluteTimeLockedAsync()
        {
            var context = new TestContext();
            await context.InitializeAsync();
            var tx = context.network.CreateTransaction();
            tx.AddInput(new TxIn());
            tx.AddOutput(new TxOut());
            Transaction.LockTimeFlags flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

            var prevheights = new List<int>();
            prevheights.Add(1);
            tx.Version = 2;
            tx.Inputs[0].PrevOut.Hash = context.txFirst[3].GetHash();
            tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            tx.Inputs[0].PrevOut.N = 0;
            tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
            tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);

            // absolute time locked
            tx.LockTime = context.chain.Tip.GetMedianTimePast().AddMinutes(1);
            tx.Inputs[0].Sequence = Sequence.Final - 1;
            prevheights[0] = context.baseheight + 4;
            context.hash = tx.GetHash();
            context.mempool.AddUnchecked(context.hash, context.entry.Time(context.DateTimeProvider.GetTime()).FromTx(tx));
            Assert.True(!MempoolValidator.CheckFinalTransaction(context.chain, context.DateTimeProvider, tx, flags)); // Locktime fails
            Assert.True(this.TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks pass

            BlockHeader blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512;
            context.chain.SetTip(blockHeader);

            blockHeader = context.network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.HashPrevBlock = context.chain.Tip.HashBlock;
            blockHeader.Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512;
            context.chain.SetTip(blockHeader);

            Assert.True(tx.IsFinal(context.chain.Tip.GetMedianTimePast().AddMinutes(2), context.chain.Tip.Height + 2)); // Locktime passes 2 min later
        }

        [Fact]
        public void GetProofOfWorkRewardForMinedBlocksTest()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateStratisPowNode(KnownNetworks.RegTest).WithDummyWallet().Start();

                TestHelper.MineBlocks(node, 10);
                node.GetProofOfWorkRewardForMinedBlocks(10).Should().Be(Money.Coins(500));

                TestHelper.MineBlocks(node, 90);
                node.GetProofOfWorkRewardForMinedBlocks(100).Should().Be(Money.Coins(5000));

                TestHelper.MineBlocks(node, 100);
                node.GetProofOfWorkRewardForMinedBlocks(200).Should().Be(Money.Coins(8725));

                TestHelper.MineBlocks(node, 200);
                node.GetProofOfWorkRewardForMinedBlocks(400).Should().Be(Money.Coins((decimal)12462.50));
            }
        }

        [Fact]
        public void Miner_Create_Block_Whilst_Connected_Syncs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(KnownNetworks.RegTest).WithDummyWallet().Start();
                CoreNode syncer = builder.CreateStratisPowNode(KnownNetworks.RegTest).Start();

                TestHelper.Connect(miner, syncer);

                TestHelper.MineBlocks(miner, 1);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(miner, syncer));
            }
        }

        [Fact]
        public void Miner_Create_Block_Whilst_Disconnected_Syncs()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(KnownNetworks.RegTest).WithDummyWallet().Start();
                CoreNode syncer = builder.CreateStratisPowNode(KnownNetworks.RegTest).Start();

                TestHelper.MineBlocks(miner, 1);

                TestHelper.ConnectAndSync(miner, syncer);
            }
        }

        //NOTE: These tests rely on CreateNewBlock doing its own self-validation!
        private void MinerCreateNewBlockValidity()
        {
            //TODO: fix this test
            // orphan in mempool, template creation fails
            //hash = tx.GetHash();
            //mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).FromTx(tx));
            //error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
            //Assert.True(error.ConsensusError == ConsensusErrors.BadBlockSigOps);
            //mempool.Clear();

            //TODO: fix this test
            // invalid (pre-p2sh) txn in mempool, template creation fails
            //tx = tx.Clone();
            //tx.Inputs[0].PrevOut.Hash = txFirst[0].GetHash();
            //tx.Inputs[0].PrevOut.N = 0;
            //tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            //tx.Outputs[0].Value = BLOCKSUBSIDY - LOWFEE;
            //script = new Script(OpcodeType.OP_1);
            //tx.Outputs[0].ScriptPubKey = new ScriptId(script).ScriptPubKey;
            //hash = tx.GetHash();
            //mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
            //tx = tx.Clone();
            //tx.Inputs[0].PrevOut.Hash = hash;
            //tx.Inputs[0].ScriptSig = new Script(script.ToBytes());
            //tx.Outputs[0].Value -= LOWFEE;
            //hash = tx.GetHash();
            //mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(false).FromTx(tx));
            //error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
            //Assert.True(error.ConsensusError == ConsensusErrors.BadMultipleCoinbase);
            //mempool.Clear();

            //TODO: fix this test
            // double spend txn pair in mempool, template creation fails
            //tx = tx.Clone();
            //tx.Inputs[0].PrevOut.Hash = txFirst[0].GetHash();
            //tx.Inputs[0].PrevOut.N = 0;
            //tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
            //tx.Outputs[0].Value = BLOCKSUBSIDY - HIGHFEE;
            //tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
            //hash = tx.GetHash();
            //mempool.AddUnchecked(hash, entry.Fee(HIGHFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
            //tx = tx.Clone();
            //tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_2);
            //hash = tx.GetHash();
            //mempool.AddUnchecked(hash, entry.Fee(HIGHFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
            //error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
            //Assert.True(error.ConsensusError == ConsensusErrors.BadMultipleCoinbase);
            //mempool.Clear();

            //TODO: fix this test
            //// subsidy changing
            //int nHeight = chain.Height;
            //// Create an actual 209999-long block chain (without valid blocks).
            //while (chain.Tip.Height < 209999)
            //{
            //    //var block = new Block();
            //    //block.Header.HashPrevBlock = chain.Tip.HashBlock;
            //    //block.Header.Version = 1;
            //    //block.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
            //    //var coinbase = new Transaction();
            //    //coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            //    //coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), new Script()));
            //    //block.AddTransaction(coinbase);
            //    //block.UpdateMerkleRoot();
            //    //block.Header.Nonce = 0;
            //    //block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
            //    //chain.SetTip(block.Header);
            //    //consensus.AcceptBlock(new ContextInformation(new BlockResult { Block = block }, network.Consensus) { CheckPow = false, CheckMerkleRoot = false });
            //    //blocks.Add(block);

            //}
            //pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
            //Assert.NotNull(pblocktemplate);

            //TODO: fix this test
            //// Extend to a 210000-long block chain.
            //while (chain.Tip.Height < 210000)
            //{
            //    var block = new Block();
            //    block.Header.HashPrevBlock = chain.Tip.HashBlock;
            //    block.Header.Version = 1;
            //    block.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
            //    var coinbase = new Transaction();
            //    coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
            //    coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), new Script()));
            //    block.AddTransaction(coinbase);
            //    block.UpdateMerkleRoot();
            //    block.Header.Nonce = 0;
            //    chain.SetTip(new ChainTipToExtand(block.Header, block.GetHash(), chain.Tip));
            //}
            //pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
            //Assert.NotNull(pblocktemplate);

            //// Delete the dummy blocks again.
            //while (chain.Tip.Height > nHeight)
            //{
            //    chain.SetTip(chain.Tip.Previous);
            //}

            //TODO: fix this test
            //    // mempool-dependent transactions (not added)
            //    tx.Inputs[0].PrevOut.Hash = hash;
            //    prevheights[0] = chainActive.Tip().Height + 1;
            //    tx.LockTime = 0;
            //    tx.Inputs[0].Sequence = 0;
            //    BOOST_CHECK(CheckFinalTx(tx, flags)); // Locktime passes
            //    BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
            //    tx.Inputs[0].Sequence = 1;
            //    BOOST_CHECK(!TestSequenceLocks(tx, flags)); // Sequence locks fail
            //    tx.Inputs[0].Sequence = CTxIn::SEQUENCE_LOCKTIME_TYPE_FLAG;
            //    BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
            //    tx.Inputs[0].Sequence = CTxIn::SEQUENCE_LOCKTIME_TYPE_FLAG | 1;
            //    BOOST_CHECK(!TestSequenceLocks(tx, flags)); // Sequence locks fail

            //    BOOST_CHECK(pblocktemplate = AssemblerForTest(consensus, network).CreateNewBlock(scriptPubKey));

            //TODO: fix this test
            //    // None of the of the absolute height/time locked tx should have made
            //    // it into the template because we still check IsFinalTx in CreateNewBlock,
            //    // but relative locked txs will if inconsistently added to mempool.
            //    // For now these will still generate a valid template until BIP68 soft fork
            //    BOOST_CHECK_EQUAL(pblocktemplate.block.vtx.size(), 3);
            //    // However if we advance height by 1 and time by 512, all of them should be mined
            //    for (int i = 0; i < CBlockIndex::nMedianTimeSpan; i++)
            //        chainActive.Tip().GetAncestor(chainActive.Tip().Height - i).Time += 512; //Trick the MedianTimePast
            //    chainActive.Tip().Height++;
            //    SetMockTime(chainActive.Tip().GetMedianTimePast() + 1);

            //    BOOST_CHECK(pblocktemplate = AssemblerForTest(consensus, network).CreateNewBlock(scriptPubKey));
            //    BOOST_CHECK_EQUAL(pblocktemplate.block.vtx.size(), 5);

            //    chainActive.Tip().Height--;
            //    SetMockTime(0);
            //    mempool.clear();
        }

        [Fact]
        public void MiningAndPropagatingPOW_MineBlockCheckNodeConsensusTipIsCorrect()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(KnownNetworks.RegTest).WithDummyWallet().Start();

                TestHelper.MineBlocks(miner, 5);

                Assert.Equal(5, miner.FullNode.ConsensusManager().Tip.Height);
            }
        }

        [Fact]
        public void Miner_PosNetwork_CreatePowTransaction_AheadOfFutureDrift_ShouldNotBeIncludedInBlock()
        {
            var network = KnownNetworks.StratisRegTest;

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisMiner = builder.CreateStratisPosNode(network).WithWallet().Start();

                int maturity = (int)network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(stratisMiner, maturity + 5);

                // Send coins to the receiver
                var context = WalletTests.CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);

                Transaction trx = stratisMiner.FullNode.WalletTransactionHandler().BuildTransaction(context);

                // This should make the mempool reject a POS trx.
                trx.Time = Utils.DateTimeToUnixTime(Utils.UnixTimeToDateTime(trx.Time).AddMinutes(5));

                // Sign trx again after changing the time property.
                trx = context.TransactionBuilder.SignTransaction(trx);

                var broadcaster = stratisMiner.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal(State.ToBroadcast, entry.State);

                Assert.NotNull(stratisMiner.FullNode.MempoolManager().GetTransaction(trx.GetHash()).Result);

                TestHelper.MineBlocks(stratisMiner, 1);

                Assert.NotNull(stratisMiner.FullNode.MempoolManager().GetTransaction(trx.GetHash()).Result);
            }
        }
    }
}
