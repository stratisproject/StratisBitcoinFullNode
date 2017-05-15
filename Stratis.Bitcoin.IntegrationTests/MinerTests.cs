using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Miner;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class MinerTests
    {
		static FeeRate blockMinFeeRate = new FeeRate(Mining.DefaultBlockMinTxFee);

	    public static BlockAssembler AssemblerForTest(ConsensusLoop consensusLoop, Network network, DateTimeProvider date, TxMempool mempool, MempoolScheduler scheduler, ConcurrentChain chain)
	    {
		    BlockAssembler.Options options = new BlockAssembler.Options();

		    options.BlockMaxWeight = network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_WEIGHT;
		    options.BlockMaxSize = network.Consensus.Option<PowConsensusOptions>().MAX_BLOCK_SERIALIZED_SIZE;
		    options.BlockMinFeeRate = blockMinFeeRate;

		    return new BlockAssembler(consensusLoop, network, chain, scheduler,mempool, date, options);
	    }
		public class Blockinfo
		{
			public int extranonce;
			public uint nonce;
		}

		long[,] blockinfoarr = 
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

	    public bool TestSequenceLocks(ChainedBlock chainedBlock, Transaction tx, Transaction.LockTimeFlags flags, LockPoints uselock)
	    {
		    return MempoolValidator.CheckSequenceLocks(chainedBlock, new MempoolValidationContext(tx, new MempoolValidationState(false)), flags, uselock, true);
	    }

		// Test suite for ancestor feerate transaction selection.
		// Implemented as an additional function, rather than a separate test case,
		// to allow reusing the blockchain created in CreateNewBlock_validity.
	    void TestPackageSelection(ConsensusLoop consensus, Network network, DateTimeProvider date, Script scriptPubKey, List<Transaction> txFirst,
		    TxMempool mempool, MempoolScheduler scheduler, ConcurrentChain chain)
	    {
		    // Test the ancestor feerate transaction selection.
		    TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();

		    // Test that a medium fee transaction will be selected after a higher fee
		    // rate package with a low fee rate parent.
		    Transaction tx = new Transaction();
		    tx.AddInput(new TxIn(new OutPoint(txFirst[0].GetHash(), 0), new Script(OpcodeType.OP_1)));
		    tx.AddOutput(new TxOut(new Money(5000000000L - 1000), new Script()));

		    // This tx has a low fee: 1000 satoshis
		    uint256 hashParentTx = tx.GetHash(); // save this txid for later use
		    mempool.AddUnchecked(hashParentTx, entry.Fee(1000).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));

		    // This tx has a medium fee: 10000 satoshis
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = txFirst[1].GetHash();
		    tx.Outputs[0].Value = 5000000000L - 10000;
		    uint256 hashMediumFeeTx = tx.GetHash();
		    mempool.AddUnchecked(hashMediumFeeTx, entry.Fee(10000).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));

		    // This tx has a high fee, but depends on the first transaction
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = hashParentTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 50k satoshi fee
		    uint256 hashHighFeeTx = tx.GetHash();
		    mempool.AddUnchecked(hashHighFeeTx, entry.Fee(50000).Time(date.GetTime()).SpendsCoinbase(false).FromTx(tx));

		    var pblocktemplate = AssemblerForTest(consensus, network, date, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[1].GetHash() == hashParentTx);
		    Assert.True(pblocktemplate.Block.Transactions[2].GetHash() == hashHighFeeTx);
		    Assert.True(pblocktemplate.Block.Transactions[3].GetHash() == hashMediumFeeTx);

		    // Test that a package below the block min tx fee doesn't get included
		    tx = tx.Clone();
		    tx.Inputs[0].PrevOut.Hash = hashHighFeeTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000; // 0 fee
		    uint256 hashFreeTx = tx.GetHash();
		    mempool.AddUnchecked(hashFreeTx, entry.Fee(0).FromTx(tx));
		    var freeTxSize = tx.GetSerializedSize();

		    // Calculate a fee on child transaction that will put the package just
		    // below the block min tx fee (assuming 1 child tx of the same size).
		    var feeToUse = blockMinFeeRate.GetFee(2*freeTxSize) - 1;

		    tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = hashFreeTx;
		    tx.Outputs[0].Value = 5000000000L - 1000 - 50000 - feeToUse;
		    uint256 hashLowFeeTx = tx.GetHash();
		    mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse).FromTx(tx));
		    pblocktemplate = AssemblerForTest(consensus, network, date, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
		    // Verify that the free tx and the low fee tx didn't get selected
		    for (var i = 0; i < pblocktemplate.Block.Transactions.Count; ++i)
		    {
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashFreeTx);
			    Assert.True(pblocktemplate.Block.Transactions[i].GetHash() != hashLowFeeTx);
		    }

		    // Test that packages above the min relay fee do get included, even if one
		    // of the transactions is below the min relay fee
		    // Remove the low fee transaction and replace with a higher fee transaction
		    mempool.RemoveRecursive(tx);
			tx = tx.Clone();
			tx.Outputs[0].Value -= 2; // Now we should be just over the min relay fee
		    hashLowFeeTx = tx.GetHash();
		    mempool.AddUnchecked(hashLowFeeTx, entry.Fee(feeToUse + 2).FromTx(tx));
		    pblocktemplate = AssemblerForTest(consensus, network, date, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[4].GetHash() == hashFreeTx);
		    Assert.True(pblocktemplate.Block.Transactions[5].GetHash() == hashLowFeeTx);

			// Test that transaction selection properly updates ancestor fee
			// calculations as ancestor transactions get included in a block.
			// Add a 0-fee transaction that has 2 outputs.
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = txFirst[2].GetHash();
		    tx.AddOutput(Money.Zero, new Script());
		    tx.Outputs[0].Value = 5000000000L - 100000000;
		    tx.Outputs[1].Value = 100000000; // 1BTC output
		    uint256 hashFreeTx2 = tx.GetHash();
		    mempool.AddUnchecked(hashFreeTx2, entry.Fee(0).SpendsCoinbase(true).FromTx(tx));

			// This tx can't be mined by itself
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = hashFreeTx2;
		    tx.Outputs.RemoveAt(1);
		    feeToUse = blockMinFeeRate.GetFee(freeTxSize);
		    tx.Outputs[0].Value = 5000000000L - 100000000 - feeToUse;
		    uint256 hashLowFeeTx2 = tx.GetHash();
		    mempool.AddUnchecked(hashLowFeeTx2, entry.Fee(feeToUse).SpendsCoinbase(false).FromTx(tx));
		    pblocktemplate = AssemblerForTest(consensus, network, date, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);

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
		    mempool.AddUnchecked(tx.GetHash(), entry.Fee(10000).FromTx(tx));
		    pblocktemplate = AssemblerForTest(consensus, network, date, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
		    Assert.True(pblocktemplate.Block.Transactions[8].GetHash() == hashLowFeeTx2);
	    }

		//NOTE: These tests rely on CreateNewBlock doing its own self-validation!
		[Fact]
		public void MinerCreateNewBlockValidity()
		{
			Logs.Configure(new LoggerFactory());

			List<Blockinfo> blockinfo = new List<Blockinfo>();
			var lst = blockinfoarr.Cast<long>().ToList();
			for (int i = 0; i < lst.Count; i += 2)
				blockinfo.Add(new Blockinfo() {extranonce = (int) lst[i], nonce = (uint)lst[i + 1]});

			// Note that by default, these tests run with size accounting enabled.
			var network = Network.RegTest;
			var hex = Encoders.Hex.DecodeData("04678afdb0fe5548271967f1a67130b7105cd6a828e03909a67962e0ea1f61deb649f6bc3f4cef38c4f35504e51ec112de5c384df7ba0b8d578a4c702b6bf11d5f");
			Script scriptPubKey = new Script(new[] {Op.GetPushOp(hex), OpcodeType.OP_CHECKSIG});
			BlockTemplate pblocktemplate = new BlockTemplate();
			Transaction tx, tx2;
			Script script;
			uint256 hash;
			TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();
			var chain = new ConcurrentChain(network);
			network.Consensus.Options = new PowConsensusOptions();
			ConsensusLoop consensus = new ConsensusLoop(new PowConsensusValidator(network), chain, new CachedCoinView(new InMemoryCoinView(chain.Tip.HashBlock)), new LookaheadBlockPuller(chain, new ConnectionManager(network, new NodeConnectionParameters(), new NodeSettings())));
			consensus.Initialize();

			entry.Fee(11);
			entry.Height(11);
			var date = new MemoryPoolTests.DateTimeProviderSet();
			date.time = DateTimeProvider.Default.GetTime();
			date.timeutc = DateTimeProvider.Default.GetUtcNow();
			var mempool = new TxMempool(new FeeRate(0), new NodeSettings());
			var	fCheckpointsEnabled = false;
			var scheduler = new MempoolScheduler();

			// Simple block creation, nothing special yet:
			pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			chain.SetTip(pblocktemplate.Block.Header);
			consensus.AcceptBlock(new ContextInformation(new BlockResult { Block = pblocktemplate.Block }, network.Consensus) {CheckPow = false, CheckMerkleRoot = false});

			// We can't make transactions until we have inputs
			// Therefore, load 100 blocks :)
			int baseheight = 0;
			List<Block> blocks = new List<Block>();
			List<Transaction> txFirst = new List<Transaction>();
			for (int i = 0; i < blockinfo.Count; ++i)
			{
				var pblock = pblocktemplate.Block.Clone(); // pointer for convenience
				pblock.Header.HashPrevBlock = chain.Tip.HashBlock;
				pblock.Header.Version = 1;
				pblock.Header.Time = Utils.DateTimeToUnixTime(chain.Tip.GetMedianTimePast()) + 1;
				Transaction txCoinbase = pblock.Transactions[0].Clone();
				txCoinbase.Inputs.Clear();
				txCoinbase.Version = 1;
				txCoinbase.AddInput(new TxIn(new Script(new[] {Op.GetPushOp(blockinfo[i].extranonce), Op.GetPushOp(chain.Height)})));
				// Ignore the (optional) segwit commitment added by CreateNewBlock (as the hardcoded nonces don't account for this)
				txCoinbase.AddOutput(new TxOut(Money.Zero, new Script()));
				pblock.Transactions[0] = txCoinbase;

				if (txFirst.Count == 0)
					baseheight = chain.Height;
				if (txFirst.Count < 4)
					txFirst.Add(pblock.Transactions[0]);
				pblock.UpdateMerkleRoot();

				pblock.Header.Nonce = blockinfo[i].nonce;

				//while (!pblock.CheckProofOfWork())
				//	pblock.Header.Nonce++;

				chain.SetTip(pblock.Header);
				consensus.AcceptBlock(new ContextInformation(new BlockResult {Block = pblock}, network.Consensus) { CheckPow = false, CheckMerkleRoot = false });
				blocks.Add(pblock);
			}

			// Just to make sure we can still make simple blocks
			pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			Assert.NotNull(pblocktemplate);

			Money BLOCKSUBSIDY = 50 * Money.COIN;
			Money LOWFEE = Money.CENT;
			Money HIGHFEE = Money.COIN;
			Money HIGHERFEE = 4 * Money.COIN;

			// block sigops > limit: 1000 CHECKMULTISIG + 1
			tx = new Transaction();
			tx.AddInput(new TxIn(new OutPoint(txFirst[0].GetHash(), 0), new Script(new byte[] { (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_0, (byte)OpcodeType.OP_NOP, (byte)OpcodeType.OP_CHECKMULTISIG, (byte)OpcodeType.OP_1 })));
			// NOTE: OP_NOP is used to force 20 SigOps for the CHECKMULTISIG
			tx.AddOutput(BLOCKSUBSIDY, new Script());
			for (int i = 0; i < 1001; ++i)
			{
				tx.Outputs[0].Value -= LOWFEE;
				hash = tx.GetHash();
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
															   // If we don't set the # of sig ops in the CTxMemPoolEntry, template creation fails
				mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = hash;
			}
			var error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
			Assert.True(error.ConsensusError == ConsensusErrors.BadBlockSigOps);
			mempool.Clear();

			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = txFirst[0].GetHash();
			tx.Outputs[0].Value = BLOCKSUBSIDY;
			for (int i = 0; i < 1001; ++i)
			{
				tx.Outputs[0].Value -= LOWFEE;
				hash = tx.GetHash();
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
															   // If we do set the # of sig ops in the CTxMemPoolEntry, template creation passes
				mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(spendsCoinbase).SigOpsCost(80).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = hash;
			}
			pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			Assert.NotNull(pblocktemplate);
			mempool.Clear();

			// block size > limit
			tx = tx.Clone();
			tx.Inputs[0].ScriptSig = new Script();
			// 18 * (520char + DROP) + OP_1 = 9433 bytes
			var vchData = new byte[520];
			for (int i = 0; i < 18; ++i)
				tx.Inputs[0].ScriptSig = new Script(tx.Inputs[0].ScriptSig.ToBytes().Concat(vchData.Concat(new[] {(byte) OpcodeType.OP_DROP})));
			tx.Inputs[0].ScriptSig = new Script(tx.Inputs[0].ScriptSig.ToBytes().Concat(new[] {(byte) OpcodeType.OP_1}));
			tx.Inputs[0].PrevOut.Hash = txFirst[0].GetHash();
			tx.Outputs[0].Value = BLOCKSUBSIDY;
			for (int i = 0; i < 128; ++i)
			{
				tx.Outputs[0].Value -= LOWFEE;
				hash = tx.GetHash();
				bool spendsCoinbase = (i == 0) ? true : false; // only first tx spends coinbase
				mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(spendsCoinbase).FromTx(tx));
				tx = tx.Clone();
				tx.Inputs[0].PrevOut.Hash = hash;
			}
			pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			Assert.NotNull(pblocktemplate);
			mempool.Clear();

			 //TODO: fix this test
			// orphan in mempool, template creation fails
			//hash = tx.GetHash();
			//mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).FromTx(tx));
			//error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
			//Assert.True(error.ConsensusError == ConsensusErrors.BadBlockSigOps);
			//mempool.Clear();

			// child with higher feerate than parent
			tx = tx.Clone();
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_1);
			tx.Inputs[0].PrevOut.Hash = txFirst[1].GetHash();
			tx.Outputs[0].Value = BLOCKSUBSIDY - HIGHFEE;
			hash = tx.GetHash();
			mempool.AddUnchecked(hash, entry.Fee(HIGHFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			tx = tx.Clone();
			tx.Inputs[0].PrevOut.Hash = hash;
			tx.Inputs.Add(new TxIn());
			tx.Inputs[1].ScriptSig = new Script(OpcodeType.OP_1);
			tx.Inputs[1].PrevOut.Hash = txFirst[0].GetHash();
			tx.Inputs[1].PrevOut.N = 0;
			tx.Outputs[0].Value = tx.Outputs[0].Value + BLOCKSUBSIDY - HIGHERFEE; //First txn output + fresh coinbase - new txn fee
			hash = tx.GetHash();
			mempool.AddUnchecked(hash, entry.Fee(HIGHERFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			pblocktemplate = AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey);
			Assert.NotNull(pblocktemplate);
			mempool.Clear();

			// coinbase in mempool, template creation fails
			tx = tx.Clone();
			tx.Inputs.RemoveAt(1);
			tx.Inputs[0].PrevOut = new OutPoint();
			tx.Inputs[0].ScriptSig = new Script(OpcodeType.OP_0, OpcodeType.OP_1);
			tx.Outputs[0].Value = 0;
			hash = tx.GetHash();
			// give it a fee so it'll get mined
			mempool.AddUnchecked(hash, entry.Fee(LOWFEE).Time(date.GetTime()).SpendsCoinbase(false).FromTx(tx));
			error = Assert.Throws<ConsensusErrorException>(() => AssemblerForTest(consensus, network, date as DateTimeProvider, mempool, scheduler, chain).CreateNewBlock(scriptPubKey));
			Assert.True(error.ConsensusError == ConsensusErrors.BadMultipleCoinbase);
			mempool.Clear();

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
			// non-final txs in mempool
			//date.time = chain.Tip.Header.Time + 1;
			////SetMockTime(chainActive.Tip().GetMedianTimePast() + 1);
			//var flags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
			//// height map
			//List<int> prevheights = new List<int>();

			//// relative height locked
			//tx.Version = 2;
			//tx.Inputs.Add(new TxIn());
			//prevheights.Add(1);
			//tx.Inputs[0].PrevOut.Hash = txFirst[0].GetHash(); // only 1 transaction
			//tx.Inputs[0].PrevOut.N = 0;
			//tx.Inputs[0].ScriptSig = new Script(OpcodeType. OP_1);
			//tx.Inputs[0].Sequence = new Sequence(chain.Tip.Height + 1); // txFirst[0] is the 2nd block
			//prevheights[0] = baseheight + 1;
			//tx.Outputs.Add(new TxOut());
			//tx.Outputs[0].Value = BLOCKSUBSIDY - HIGHFEE;
			//tx.Outputs[0].ScriptPubKey = new Script(OpcodeType.OP_1);
			//tx.LockTime = 0;
			//hash = tx.GetHash();
			//mempool.AddUnchecked(hash, entry.Fee(HIGHFEE).Time(date.GetTime()).SpendsCoinbase(true).FromTx(tx));
			//Assert.True(MempoolValidator.CheckFinalTransaction(chain, date, tx, flags)); // Locktime passes
			//Assert.True(!TestSequenceLocks(chain.Tip, tx, flags, new LockPoints {Height = chain.Tip.Height + 2})); // Sequence locks fail
			//var locks = tx.CalculateSequenceLocks(prevheights.ToArray(), chain.Tip, flags);
			//chain.SetTip(new BlockHeader() { HashPrevBlock = chain.Tip.HashBlock});
			//chain.SetTip(new BlockHeader() { HashPrevBlock = chain.Tip.HashBlock });
			//Assert.True(locks.Evaluate(chain.Tip)); // Sequence locks pass on 2nd block

			//TODO: fix this test
			//	// relative time locked
			//	tx.Inputs[0].PrevOut.Hash = txFirst[1].GetHash();
			//	tx.Inputs[0].Sequence = CTxIn::SEQUENCE_LOCKTIME_TYPE_FLAG | (((chainActive.Tip().GetMedianTimePast() + 1 - chainActive[1].GetMedianTimePast()) >> CTxIn::SEQUENCE_LOCKTIME_GRANULARITY) + 1); // txFirst[1] is the 3rd block
			//	prevheights[0] = baseheight + 2;
			//	hash = tx.GetHash();
			//	mempool.AddUnchecked(hash, entry.Time(date.GetTime()).FromTx(tx));
			//	BOOST_CHECK(CheckFinalTx(tx, flags)); // Locktime passes
			//	BOOST_CHECK(!TestSequenceLocks(tx, flags)); // Sequence locks fail

			//	for (int i = 0; i < CBlockIndex::nMedianTimeSpan; i++)
			//		chainActive.Tip().GetAncestor(chainActive.Tip().Height - i).Time += 512; //Trick the MedianTimePast
			//	BOOST_CHECK(SequenceLocks(tx, flags, &prevheights, CreateBlockIndex(chainActive.Tip().Height + 1))); // Sequence locks pass 512 seconds later
			//	for (int i = 0; i < CBlockIndex::nMedianTimeSpan; i++)
			//		chainActive.Tip().GetAncestor(chainActive.Tip().Height - i).Time -= 512; //undo tricked MTP

			//	// absolute height locked
			//	tx.Inputs[0].PrevOut.Hash = txFirst[2].GetHash();
			//	tx.Inputs[0].Sequence = CTxIn::SEQUENCE_FINAL - 1;
			//	prevheights[0] = baseheight + 3;
			//	tx.LockTime = chainActive.Tip().Height + 1;
			//	hash = tx.GetHash();
			//	mempool.AddUnchecked(hash, entry.Time(date.GetTime()).FromTx(tx));
			//	BOOST_CHECK(!CheckFinalTx(tx, flags)); // Locktime fails
			//	BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
			//	BOOST_CHECK(IsFinalTx(tx, chainActive.Tip().Height + 2, chainActive.Tip().GetMedianTimePast())); // Locktime passes on 2nd block

			//	// absolute time locked
			//	tx.Inputs[0].PrevOut.Hash = txFirst[3].GetHash();
			//	tx.LockTime = chainActive.Tip().GetMedianTimePast();
			//	prevheights.resize(1);
			//	prevheights[0] = baseheight + 4;
			//	hash = tx.GetHash();
			//	mempool.AddUnchecked(hash, entry.Time(date.GetTime()).FromTx(tx));
			//	BOOST_CHECK(!CheckFinalTx(tx, flags)); // Locktime fails
			//	BOOST_CHECK(TestSequenceLocks(tx, flags)); // Sequence locks pass
			//	BOOST_CHECK(IsFinalTx(tx, chainActive.Tip().Height + 2, chainActive.Tip().GetMedianTimePast() + 1)); // Locktime passes 1 second later

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

			TestPackageSelection(consensus, network, date as DateTimeProvider, scriptPubKey, txFirst, mempool, scheduler, chain);

			fCheckpointsEnabled = true;
		}
    }
}
