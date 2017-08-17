using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Deployments;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.Miner;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class MinerTests
    {
		static FeeRate blockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee);

	    public static PowBlockAssembler AssemblerForTest(TestContext testContext)
	    {
		    AssemblerOptions options = new AssemblerOptions();

		    options.BlockMaxWeight = testContext.network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_WEIGHT;
		    options.BlockMaxSize = testContext.network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_SERIALIZED_SIZE;
		    options.BlockMinFeeRate = blockMinFeeRate;

		    return new PowBlockAssembler(testContext.consensus, testContext.network, testContext.chain, testContext.scheduler, testContext.mempool, testContext.date, NullLogger.Instance, options);
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

		public ChainedBlock CreateBlockIndex(ChainedBlock prev)
	    {
		    ChainedBlock index = new ChainedBlock(new BlockHeader(), new BlockHeader().GetHash(), prev );
		    return index;
	    }

	    public bool TestSequenceLocks(TestContext testContext, ChainedBlock chainedBlock, Transaction tx, Transaction.LockTimeFlags flags, LockPoints uselock = null)
	    {
		    var context = new MempoolValidationContext(tx, new MempoolValidationState(false));
		    context.View = new MempoolCoinView(testContext.cachedCoinView, testContext.mempool, testContext.scheduler, null);
			context.View.LoadView(tx).GetAwaiter().GetResult();
			return MempoolValidator.CheckSequenceLocks(chainedBlock, context, flags, uselock, false);
	    }

		public class TestContext
		{
			public List<Blockinfo> blockinfo;
			public Network network;
			public Script scriptPubKey;
			public BlockTemplate newBlock;
			public Transaction tx, tx2;
			public Script script;
			public uint256 hash;
			public TestMemPoolEntryHelper entry;
			public ConcurrentChain chain;
			public ConsensusLoop consensus;
			public DateTimeProvider date;
			public TxMempool mempool;
			public MempoolAsyncLock scheduler;
			public List<Transaction> txFirst;
			public Money BLOCKSUBSIDY = 50 * Money.COIN;
			public Money LOWFEE = Money.CENT;
			public Money HIGHFEE = Money.COIN;
			public Money HIGHERFEE = 4 * Money.COIN;
			public int baseheight;
			public CachedCoinView cachedCoinView;

			public TestContext()
			{
				this.blockinfo = new List<Blockinfo>();
				var lst = blockinfoarr.Cast<long>().ToList();
				for (int i = 0; i < lst.Count; i += 2)
                    this.blockinfo.Add(new Blockinfo() { extranonce = (int)lst[i], nonce = (uint)lst[i + 1] });

                // Note that by default, these tests run with size accounting enabled.
                this.network = Network.Main;
				var hex = Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f");
				this.scriptPubKey = new Script(new[] { Op.GetPushOp(hex), OpcodeType.OP_CHECKSIG });
                this.newBlock = new BlockTemplate();

			    this.entry = new TestMemPoolEntryHelper();
			    this.chain = new ConcurrentChain(network);
			    this.network.Consensus.Options = new PowConsensusOptions();
                this.cachedCoinView = new CachedCoinView(new InMemoryCoinView(chain.Tip.HashBlock));
			    this.consensus = new ConsensusLoop(new PowConsensusValidator(network), chain, cachedCoinView, new LookaheadBlockPuller(chain, new ConnectionManager(network, new NodeConnectionParameters(), new NodeSettings(), new LoggerFactory()), new LoggerFactory()), new NodeDeployments(this.network));
			    this.consensus.Initialize();

				this.entry.Fee(11);
                this.entry.Height(11);
				var date1 = new MemoryPoolTests.DateTimeProviderSet();
				date1.time = DateTimeProvider.Default.GetTime();
				date1.timeutc = DateTimeProvider.Default.GetUtcNow();
				this.date = date1;
                this.mempool = new TxMempool(new FeeRate(1000), DateTimeProvider.Default, new BlockPolicyEstimator(new FeeRate(1000), NodeSettings.Default(), new LoggerFactory()), new LoggerFactory()); ;
                this.scheduler = new MempoolAsyncLock();

                // Simple block creation, nothing special yet:
                this.newBlock = AssemblerForTest(this).CreateNewBlock(this.scriptPubKey);
				this.chain.SetTip(this.newBlock.Block.Header);
				this.consensus.AcceptBlock(new ContextInformation(new BlockResult { Block = this.newBlock.Block }, this.network.Consensus) { CheckPow = false, CheckMerkleRoot = false });

				// We can't make transactions until we have inputs
				// Therefore, load 100 blocks :)
				this.baseheight = 0;
				List<Block> blocks = new List<Block>();
                this.txFirst = new List<Transaction>();
				for (int i = 0; i < this.blockinfo.Count; ++i)
				{
					var pblock = this.newBlock.Block.Clone(); // pointer for convenience
					pblock.Header.HashPrevBlock = this.chain.Tip.HashBlock;
					pblock.Header.Version = 1;
					pblock.Header.Time = Utils.DateTimeToUnixTime(this.chain.Tip.GetMedianTimePast()) + 1;
					Transaction txCoinbase = pblock.Transactions[0].Clone();
					txCoinbase.Inputs.Clear();
					txCoinbase.Version = 1;
					txCoinbase.AddInput(new TxIn(new Script(new[] { Op.GetPushOp(this.blockinfo[i].extranonce), Op.GetPushOp(this.chain.Height) })));
					// Ignore the (optional) segwit commitment added by CreateNewBlock (as the hardcoded nonces don't account for this)
					txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
					pblock.Transactions[0] = txCoinbase;

					if (this.txFirst.Count == 0)
						this.baseheight = this.chain.Height;
					if (this.txFirst.Count < 4)
                        this.txFirst.Add(pblock.Transactions[0]);
					pblock.UpdateMerkleRoot();

					pblock.Header.Nonce = this.blockinfo[i].nonce;

                    this.chain.SetTip(pblock.Header);
					this.consensus.AcceptBlock(new ContextInformation(new BlockResult { Block = pblock }, this.network.Consensus) { CheckPow = false, CheckMerkleRoot = false });
					blocks.Add(pblock);
				}

                // Just to make sure we can still make simple blocks
                this.newBlock = AssemblerForTest(this).CreateNewBlock(this.scriptPubKey);
				Assert.NotNull(this.newBlock);
			}
		}

		// Test suite for ancestor feerate transaction selection.
		// Implemented as an additional function, rather than a separate test case,
		// to allow reusing the blockchain created in CreateNewBlock_validity.
		[Fact]
		public void MinerTestPackageSelection()
	    {
			var context = new TestContext();

		    // Test the ancestor feerate transaction selection.
		    TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();

		    // Test that a medium fee transaction will be selected after a higher fee
		    // rate package with a low fee rate parent.
		    Transaction tx = new Transaction();
		    tx.AddInput(new TxIn(new OutPoint(context.txFirst[0].GetHash(), 0), new Script(OpcodeType.OP_1)));
		    tx.AddOutput(new TxOut(new Money(5000000000L - 1000), new Script()));

		    // This tx has a low fee: 1000 satoshis
		    uint256 hashParentTx = tx.GetHash(); // save this txid for later use
			context.mempool.AddUnchecked(hashParentTx, entry.Fee(1000).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(tx));

		    // This tx has a medium fee: 10000 satoshis
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
		    tx.Outputs[0].Value = 5000000000L - 10000;
		    uint256 hashMediumFeeTx = tx.GetHash();
			context.mempool.AddUnchecked(hashMediumFeeTx, entry.Fee(10000).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(tx));

		    // This tx has a high fee, but depends on the first transaction
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = hashParentTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 50k satoshi fee
		    uint256 hashHighFeeTx = tx.GetHash();
			context.mempool.AddUnchecked(hashHighFeeTx, entry.Fee(50000).Time(context.date.GetTime()).SpendsCoinbase(false).FromTx(tx));

		    var pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[1].GetHash() == hashParentTx);
		    Assert.True(pblocktemplate.Block.Transactions[2].GetHash() == hashHighFeeTx);
		    Assert.True(pblocktemplate.Block.Transactions[3].GetHash() == hashMediumFeeTx);

		    // Test that a package below the block min tx fee doesn't get included
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = hashHighFeeTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 0 fee
		    uint256 hashFreeTx = tx.GetHash();
			context.mempool.AddUnchecked(hashFreeTx, entry.Fee(0).FromTx(tx));
		    var freeTxSize = tx.GetSerializedSize();

		    // Calculate a fee on child transaction that will put the package just
		    // below the block min tx fee (assuming 1 child tx of the same size).
		    var feeToUse = blockMinFeeRate.GetFee(2*freeTxSize) - 1;

		    tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = hashFreeTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000 - feeToUse;
		    uint256 hashLowFeeTx = tx.GetHash();
			context.mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse).FromTx(tx));
		    pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
		    // Verify that the free tx and the low fee tx didn't get selected
		    for (var i = 0; i < pblocktemplate.Block.Transactions.Count; ++i)
		    {
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashFreeTx);
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashLowFeeTx);
		    }

			// Test that packages above the min relay fee do get included, even if one
			// of the transactions is below the min relay fee
			// Remove the low fee transaction and replace with a higher fee transaction
			context.mempool.RemoveRecursive(tx);
			tx = tx.Clone();
			tx.Outputs[0].Value -= 2; // Now we should be just over the min relay fee
		    hashLowFeeTx = tx.GetHash();
			context.mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse + 2).FromTx(tx));
		    pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[4].GetHash() == hashFreeTx);
		    Assert.True(pblocktemplate.Block.Transactions[5].GetHash() == hashLowFeeTx);

			// Test that transaction selection properly updates ancestor fee
			// calculations as ancestor transactions get included in a block.
			// Add a 0-fee transaction that has 2 outputs.
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = context.txFirst[2].GetHash();
		    tx.AddOutput(Money.Zero, new Script());
		    tx.Outputs[0].Value = 5000000000L - 100000000;
		    tx.Outputs[1].Value = 100000000; // 1BTC output
		    uint256 hashFreeTx2 = tx.GetHash();
			context.mempool.AddUnchecked(hashFreeTx2, entry.Fee(0).SpendsCoinbase(true).FromTx(tx));

			// This tx can't be mined by itself
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = hashFreeTx2;
		    tx.Outputs.RemoveAt(1);
		    feeToUse = blockMinFeeRate.GetFee(freeTxSize);
		    tx.Outputs[0].Value = 5000000000L - 100000000 - feeToUse;
		    uint256 hashLowFeeTx2 = tx.GetHash();
			context.mempool.AddUnchecked(hashLowFeeTx2, entry.Fee(feeToUse).SpendsCoinbase(false).FromTx(tx));
		    pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);

		    // Verify that this tx isn't selected.
		    for (var i = 0; i < pblocktemplate.Block.Transactions.Count; ++i)
		    {
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashFreeTx2);
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashLowFeeTx2);
		    }

			// This tx will be mineable, and should cause hashLowFeeTx2 to be selected
			// as well.
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.N = 1;
		    tx.Outputs[0].Value = 100000000 - 10000; // 10k satoshi fee
			context.mempool.AddUnchecked(tx.GetHash(), entry.Fee(10000).FromTx(tx));
		    pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[8].GetHash() == hashLowFeeTx2);
	    }

	    [Fact]
	    public void MinerCreateBlockSigopsLimit1000()
	    {
		    var context = new TestContext();

			// block sigops > limit: 1000 CHECKMULTISIG + 1
			var tx = new Transaction();
			tx.AddInput(new TxIn(new OutPoint(context.txFirst[0].GetHash(), 0), new Script(new byte[] { (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_NOP, (byte)OpcodeType.OP_CHECKMULTISIG, (byte)OpcodeType.OP_1 })));
			// NOTE: OP_NOP is used to force 20 SigOps for the CHECKMULTISIG
			tx.AddOutput(context.BLOCKSUBSIDY, new Script());
			for (int i = 0; i < 1001; ++i)
			{
				tx.Outputs[0].Value -= context.LOWFEE;
				context.hash = tx.GetHash();
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
															   // If we don't set the # of sig ops in the CTxMemPoolEntry, template creation fails
				context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.LOWFEE).Time(context.date.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = context.hash;
			}
			var error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(context).CreateNewBlock(context.scriptPubKey));
			Assert.True(error.ConsensusError == ConsensusErrors.BadBlockSigOps);
			context.mempool.Clear();

			tx.Inputs[0].PrevOut.Hash = context.txFirst[0].GetHash();
			tx.Outputs[0].Value = context.BLOCKSUBSIDY;
			for (int i = 0; i < 1001; ++i)
			{
				tx.Outputs[0].Value -= context.LOWFEE;
				context.hash = tx.GetHash();
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
															   // If we do set the # of sig ops in the CTxMemPoolEntry, template creation passes
				context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.LOWFEE).Time(context.date.GetTime()).SpendsCoinbase(spendsCoinbase).SigOpsCost(80).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = context.hash;
			}
			var pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
			Assert.NotNull(pblocktemplate);
			context.mempool.Clear();
		}

		[Fact]
		public void MinerCreateBlockSizeGreaterThenLimit()
		{
			var context = new TestContext();
			var tx = new Transaction();
			tx.AddInput(new TxIn());
			tx.AddOutput(new TxOut());

			// block size > limit
			tx = new Transaction();
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
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
				context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.LOWFEE).Time(context.date.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = context.hash;
			}
			var pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
			Assert.NotNull(pblocktemplate);
			context.mempool.Clear();
		}

	    [Fact]
	    public void MinerCreateBlockChildWithHigherFeerateThanParent()
	    {
		    var context = new TestContext();
		    var tx = new Transaction();
		    tx.AddInput(new TxIn());
		    tx.AddOutput(new TxOut());

			// child with higher feerate than parent
			tx = tx.Clone();
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
			tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
			tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
			context.hash = tx.GetHash();
			context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHFEE).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = context.hash;
			tx.Inputs.Add(new TxIn());
			tx.Inputs[1].ScriptSig = new Script(OpcodeType.OP_1);
			tx.Inputs[1].PrevOut.Hash = context.txFirst[0].GetHash();
			tx.Inputs[1].PrevOut.N = 0;
			tx.Outputs[0].Value = tx.Outputs[0].Value + context.BLOCKSUBSIDY - context.HIGHERFEE; //First txn output + fresh coinbase - new txn fee
			context.hash = tx.GetHash();
			context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHERFEE).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			var pblocktemplate = AssemblerForTest(context).CreateNewBlock(context.scriptPubKey);
			Assert.NotNull(pblocktemplate);
			context.mempool.Clear();
		}

	    [Fact]
	    public void MinerCreateBlockCoinbaseMempoolTemplateCreationFails()
	    {
		    var context = new TestContext();
		    var tx = new Transaction();
		    tx.AddInput(new TxIn());
		    tx.AddOutput(new TxOut());

			// coinbase in mempool, template creation fails
			tx.Inputs[0].PrevOut = new OutPoint();
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_0, OpcodeType.OP_1);
			tx.Outputs[0].Value = 0;
			context.hash = tx.GetHash();
			// give it a fee so it'll get mined
			context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.LOWFEE).Time(context.date.GetTime()).SpendsCoinbase(false).FromTx(tx));
			var error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(context).CreateNewBlock(context.scriptPubKey));
			Assert.True(error.ConsensusError == ConsensusErrors.BadMultipleCoinbase);
			context.mempool.Clear();
		}

		[Fact]
		public void MinerCreateBlockNonFinalTxsInMempool()
		{
			var context = new TestContext();
			var tx = new Transaction();
			tx.AddInput(new TxIn());
			tx.AddOutput(new TxOut());

			// non - final txs in mempool
			(context.date as MemoryPoolTests.DateTimeProviderSet).time = context.chain.Tip.Header.Time + 1;
			//SetMockTime(chainActive.Tip().GetMedianTimePast() + 1);
			var flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
			// height map
			List<int> prevheights = new List<int>();

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
			context.mempool.AddUnchecked(context.hash, context.entry.Fee(context.HIGHFEE).Time(context.date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			Assert.True(MempoolValidator.CheckFinalTransaction(context.chain, context.date, tx, flags)); // Locktime passes
			Assert.True(!TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks fail
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1 });
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1 });
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 1 });
			var locks = tx.CalculateSequenceLocks(prevheights.ToArray(), context.chain.Tip, flags);
			Assert.True(locks.Evaluate(context.chain.Tip)); // Sequence locks pass on 2nd block
		}

		[Fact]
		public void MinerCreateBlockRelativeTimeLocked()
		{
			var context = new TestContext();
			var tx = new Transaction();
			tx.AddInput(new TxIn());
			tx.AddOutput(new TxOut());

			var flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

			// height map
			List<int> prevheights = new List<int>();
			prevheights.Add(1);
			// relative time locked
			tx.Version = 2;
			tx.Inputs[0].PrevOut.Hash = context.txFirst[1].GetHash();
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
			tx.Inputs[0].PrevOut.N = 0;
			tx.Inputs[0].Sequence =  new Sequence(TimeSpan.FromMinutes(10)); // txFirst[1] is the 3rd block
			tx.Outputs[0].Value = context.BLOCKSUBSIDY - context.HIGHFEE;
			tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
			tx.LockTime = 0;
			prevheights[0] = context.baseheight + 2;
			context.hash = tx.GetHash();
			context.mempool.AddUnchecked(context.hash, context.entry.Time(context.date.GetTime()).FromTx(tx));
			Assert.True(MempoolValidator.CheckFinalTransaction(context.chain, context.date, tx, flags)); // Locktime passes
			Assert.True(!TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks fail
		}

	    [Fact]
	    public void MinerCreateBlockAbsoluteHeightLocked()
	    {
		    var context = new TestContext();
		    var tx = new Transaction();
		    tx.AddInput(new TxIn());
		    tx.AddOutput(new TxOut());
			var flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

			int MedianTimeSpan = 11;
			List<int> prevheights = new List<int>();
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
				context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512 });
		    var locks = (tx.CalculateSequenceLocks(prevheights.ToArray(), context.chain.Tip, flags));
			Assert.True(locks.Evaluate(context.chain.Tip));

			context = new TestContext();

			// absolute height locked
			tx.Inputs[0].PrevOut.Hash = context.txFirst[2].GetHash();
			tx.Inputs[0].Sequence = Sequence.Final - 1;
			prevheights[0] = context.baseheight + 3;
			tx.LockTime = context.chain.Tip.Height + 1;
			context.hash = tx.GetHash();
			context.mempool.AddUnchecked(context.hash, context.entry.Time(context.date.GetTime()).FromTx(tx));
			Assert.True(!MempoolValidator.CheckFinalTransaction(context.chain, context.date, tx, flags)); // Locktime fails
			Assert.True(TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks pass
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512 });
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512 });
			Assert.True(tx.IsFinal(context.chain.Tip.GetMedianTimePast(), context.chain.Tip.Height + 2)); // Locktime passes on 2nd block
		}

		[Fact]
		public void MinerCreateBlockAbsoluteTimeLocked()
	    {
			var context = new TestContext();
			var tx = new Transaction();
			tx.AddInput(new TxIn());
			tx.AddOutput(new TxOut());
			var flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;

			List<int> prevheights = new List<int>();
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
			context.mempool.AddUnchecked(context.hash, context.entry.Time(context.date.GetTime()).FromTx(tx));
			Assert.True(!MempoolValidator.CheckFinalTransaction(context.chain, context.date, tx, flags)); // Locktime fails
			Assert.True(TestSequenceLocks(context, context.chain.Tip, tx, flags)); // Sequence locks pass
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512 });
			context.chain.SetTip(new BlockHeader() { HashPrevBlock = context.chain.Tip.HashBlock, Time = Utils.DateTimeToUnixTime(context.chain.Tip.GetMedianTimePast()) + 512 });
			Assert.True(tx.IsFinal(context.chain.Tip.GetMedianTimePast().AddMinutes(2), context.chain.Tip.Height + 2)); // Locktime passes 2 min later

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
			//	//var block = new Block();
			//	//block.Header.HashPrevBlock = chain.Tip.HashBlock;
			//	//block.Header.Version = 1;
			//	//block.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
			//	//var coinbase = new Transaction();
			//	//coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
			//	//coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), new Script()));
			//	//block.AddTransaction(coinbase);
			//	//block.UpdateMerkleRoot();
			//	//block.Header.Nonce = 0;
			//	//block.Header.Bits = block.Header.GetWorkRequired(network, chain.Tip);
			//	//chain.SetTip(block.Header);
			//	//consensus.AcceptBlock(new ContextInformation(new BlockResult { Block = block }, network.Consensus) { CheckPow = false, CheckMerkleRoot = false });
			//	//blocks.Add(block);

			//}
			//pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			//Assert.NotNull(pblocktemplate);

			//TODO: fix this test
			//// Extend to a 210000-long block chain.
			//while (chain.Tip.Height < 210000)
			//{
			//	var block = new Block();
			//	block.Header.HashPrevBlock = chain.Tip.HashBlock;
			//	block.Header.Version = 1;
			//	block.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
			//	var coinbase = new Transaction();
			//	coinbase.AddInput(TxIn.CreateCoinbase(chain.Height + 1));
			//	coinbase.AddOutput(new TxOut(network.GetReward(chain.Height + 1), new Script()));
			//	block.AddTransaction(coinbase);
			//	block.UpdateMerkleRoot();
			//	block.Header.Nonce = 0;
			//	chain.SetTip(new ChainedBlock(block.Header, block.GetHash(), chain.Tip));
			//}
			//pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			//Assert.NotNull(pblocktemplate);

			//// Delete the dummy blocks again.
			//while (chain.Tip.Height > nHeight)
			//{
			//	chain.SetTip(chain.Tip.Previous);
			//}

			//TODO: fix this test
			//	// mempool-dependent transactions (not added)
			//	tx.Inputs[0].PrevOut.Hash = hash;
			//	prevheights[0] = chainActive.Tip().Height + 1;
			//	tx.LockTime = 0;
			//	tx.Inputs[0].Sequence = 0;
			//	BOOST_CHECK(CheckFinalTx(tx, flags)); // Locktime passes
			//	BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
			//	tx.Inputs[0].Sequence = 1;
			//	BOOST_CHECK(!TestSequenceLocks(tx, flags)); // Sequence locks fail
			//	tx.Inputs[0].Sequence = CTxIn::SEQUENCE_LOCKTIME_TYPE_FLAG;
			//	BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
			//	tx.Inputs[0].Sequence = CTxIn::SEQUENCE_LOCKTIME_TYPE_FLAG | 1;
			//	BOOST_CHECK(!TestSequenceLocks(tx, flags)); // Sequence locks fail

			//	BOOST_CHECK(pblocktemplate = AssemblerForTest(consensus, network).CreateNewBlock(scriptPubKey));

			//TODO: fix this test
			//	// None of the of the absolute height/time locked tx should have made
			//	// it into the template because we still check IsFinalTx in CreateNewBlock,
			//	// but relative locked txs will if inconsistently added to mempool.
			//	// For now these will still generate a valid template until BIP68 soft fork
			//	BOOST_CHECK_EQUAL(pblocktemplate.block.vtx.size(), 3);
			//	// However if we advance height by 1 and time by 512, all of them should be mined
			//	for (int i = 0; i < CBlockIndex::nMedianTimeSpan; i++)
			//		chainActive.Tip().GetAncestor(chainActive.Tip().Height - i).Time += 512; //Trick the MedianTimePast
			//	chainActive.Tip().Height++;
			//	SetMockTime(chainActive.Tip().GetMedianTimePast() + 1);

			//	BOOST_CHECK(pblocktemplate = AssemblerForTest(consensus, network).CreateNewBlock(scriptPubKey));
			//	BOOST_CHECK_EQUAL(pblocktemplate.block.vtx.size(), 5);

			//	chainActive.Tip().Height--;
			//	SetMockTime(0);
			//	mempool.clear();

		}
	}
}
