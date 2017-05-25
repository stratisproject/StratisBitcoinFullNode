using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class StakeValidator
	{
		private readonly Network network;
		private readonly StakeChain stakeChain;
		private readonly ConcurrentChain chain;
		private readonly CoinView coinView;
		private readonly PosConsensusOptions consensusOptions;

		public StakeValidator(Network network,
			StakeChain stakeChain, ConcurrentChain chain, CoinView coinView)
		{
			this.network = network;
			this.stakeChain = stakeChain;
			this.chain = chain;
			this.coinView = coinView;
			this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
		}

		public void CheckProofOfStake(ContextInformation context, ChainedBlock pindexPrev, BlockStake prevBlockStake,
			Transaction tx, uint nBits)
		{
			if (!tx.IsCoinStake)
				ConsensusErrors.NonCoinstake.Throw();

			// Kernel (input 0) must match the stake hash target per coin age (nBits)
			var txIn = tx.Inputs[0];

			// First try finding the previous transaction in database
			var coins = coinView.FetchCoinsAsync(new[] {txIn.PrevOut.Hash}).GetAwaiter().GetResult();
			if (coins == null || coins.UnspentOutputs.Length != 1)
				ConsensusErrors.ReadTxPrevFailed.Throw();

			var prevBlock = chain.GetBlock(coins.BlockHash);
			var prevUtxo = coins.UnspentOutputs[0];

			// Verify signature
			if (!this.VerifySignature(prevUtxo, tx, 0, ScriptVerify.None))
				ConsensusErrors.CoinstakeVerifySignatureFailed.Throw();

			// Min age requirement
			if (IsProtocolV3((int) tx.Time))
			{
				if (IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
					ConsensusErrors.InvalidStakeDepth.Throw();
			}
			else
			{
				var nTimeBlockFrom = prevBlock.Header.Time;
				if (nTimeBlockFrom + this.consensusOptions.StakeMinAge > tx.Time)
					ConsensusErrors.MinAgeViolation.Throw();
			}

			this.CheckStakeKernelHash(context, pindexPrev, nBits, prevBlock, prevUtxo, prevBlockStake, txIn.PrevOut, tx.Time);
		}

		private static bool IsConfirmedInNPrevBlocks(UnspentOutputs utxoSet, ChainedBlock pindexFrom, long maxDepth)
		{
			var actualDepth = pindexFrom.Height - (int) utxoSet.Height;

			if (actualDepth < maxDepth)
				return true;

			return false;
		}

		private bool VerifySignature(UnspentOutputs txFrom, Transaction txTo, int txToInN, ScriptVerify flagScriptVerify)
		{
			var input = txTo.Inputs[txToInN];

			if (input.PrevOut.N >= txFrom._Outputs.Length)
				return false;

			if (input.PrevOut.Hash != txFrom.TransactionId)
				return false;

			var output = txFrom._Outputs[input.PrevOut.N];

			var txData = new PrecomputedTransactionData(txTo);
			var checker = new TransactionChecker(txTo, txToInN, output.Value, txData);
			var ctx = new ScriptEvaluationContext {ScriptVerify = flagScriptVerify};

			return ctx.VerifyScript(input.ScriptSig, output.ScriptPubKey, checker);
		}

		private void CheckStakeKernelHash(ContextInformation context, ChainedBlock pindexPrev, uint nBits, ChainedBlock blockFrom,
			UnspentOutputs txPrev, BlockStake prevBlockStake, OutPoint prevout, uint nTimeTx)
		{
			if (IsProtocolV2(pindexPrev.Height + 1))
				this.CheckStakeKernelHashV2(context, pindexPrev, nBits, blockFrom.Header.Time, prevBlockStake, txPrev, prevout, nTimeTx);
			else
				this.CheckStakeKernelHashV1();
		}

		// Stratis kernel protocol
		// coinstake must meet hash target according to the protocol:
		// kernel (input 0) must meet the formula
		//     hash(nStakeModifier + txPrev.block.nTime + txPrev.nTime + txPrev.vout.hash + txPrev.vout.n + nTime) < bnTarget * nWeight
		// this ensures that the chance of getting a coinstake is proportional to the
		// amount of coins one owns.
		// The reason this hash is chosen is the following:
		//   nStakeModifier: scrambles computation to make it very difficult to precompute
		//                   future proof-of-stake
		//   txPrev.block.nTime: prevent nodes from guessing a good timestamp to
		//                       generate transaction for future advantage,
		//                       obsolete since v3
		//   txPrev.nTime: slightly scrambles computation
		//   txPrev.vout.hash: hash of txPrev, to reduce the chance of nodes
		//                     generating coinstake at the same time
		//   txPrev.vout.n: output number of txPrev, to reduce the chance of nodes
		//                  generating coinstake at the same time
		//   nTime: current timestamp
		//   block/tx hash should not be used here as they can be generated in vast
		//   quantities so as to generate blocks faster, degrading the system back into
		//   a proof-of-work situation.
		//
		private void CheckStakeKernelHashV1()
		{
			// this is not relevant for the stratis blockchain
			throw new NotImplementedException();
		}

		//private static uint256 ToUInt256_(BigInteger input)
		//{
		//	var nSize = input.BitLength; 
		//	if (nSize < 4)
		//		return 0;
		//	var vch = input.ToByteArray();
		//	//if (vch.Count() > 4)
		//	//	vch[4] &= 0x7f;
		//	vch = vch.Reverse().ToArray();
		//	uint256 n = uint256.Zero;
		//	var b = n.ToBytes();
		//	var count = vch.Length - 1;
		//	var start = vch.Length < 32 ? 1 : 0;
		//	var finish = vch.Length > 32 ? 5 : 4;
		//	for (int i = start, j = count; i < n.Size && j >= finish; i++, j--)
		//		b[i] = vch[j];
		//	return new uint256(b, false);
		//}

		private static uint256 ToUInt256(BigInteger input)
		{
			var array = input.ToByteArray();

			var missingZero = 32 - array.Length;
			if (missingZero < 0)
			{
				//throw new InvalidOperationException("Awful bug, this should never happen");
				array = array.Skip(Math.Abs(missingZero)).ToArray();
			}
			if (missingZero > 0)
			{
				array = new byte[missingZero].Concat(array).ToArray();
			}
			return new uint256(array, false);
		}

		private static BigInteger FromUInt256(uint256 input)
		{
			return BigInteger.Zero;
		}

		// Stratis kernel protocol
		// coinstake must meet hash target according to the protocol:
		// kernel (input 0) must meet the formula
		//     hash(nStakeModifier + txPrev.block.nTime + txPrev.nTime + txPrev.vout.hash + txPrev.vout.n + nTime) < bnTarget * nWeight
		// this ensures that the chance of getting a coinstake is proportional to the
		// amount of coins one owns.
		// The reason this hash is chosen is the following:
		//   nStakeModifier: scrambles computation to make it very difficult to precompute
		//                   future proof-of-stake
		//   txPrev.block.nTime: prevent nodes from guessing a good timestamp to
		//                       generate transaction for future advantage,
		//                       obsolete since v3
		//   txPrev.nTime: slightly scrambles computation
		//   txPrev.vout.hash: hash of txPrev, to reduce the chance of nodes
		//                     generating coinstake at the same time
		//   txPrev.vout.n: output number of txPrev, to reduce the chance of nodes
		//                  generating coinstake at the same time
		//   nTime: current timestamp
		//   block/tx hash should not be used here as they can be generated in vast
		//   quantities so as to generate blocks faster, degrading the system back into
		//   a proof-of-work situation.
		//
		private void CheckStakeKernelHashV2(ContextInformation context, ChainedBlock pindexPrev, uint nBits,
			uint nTimeBlockFrom,
			BlockStake prevBlockStake, UnspentOutputs txPrev, OutPoint prevout, uint nTimeTx)
		{
			if (nTimeTx < txPrev.Time)
				ConsensusErrors.StakeTimeViolation.Throw();

			// Base target
			var bnTarget = new Target(nBits).ToBigInteger();

			// TODO: Investigate:
			// The POS protocol should probably put a limit on the max amount that can be staked
			// not a hard limit but a limit that allow any amount to be staked with a max weight value.
			// the max weight should not exceed the max uint256 array size (array siez = 32)
			
			// Weighted target
			var nValueIn = txPrev._Outputs[prevout.N].Value.Satoshi;
			var bnWeight = BigInteger.ValueOf(nValueIn);
			bnTarget = bnTarget.Multiply(bnWeight);

			context.Stake.TargetProofOfStake = ToUInt256(bnTarget);

			var nStakeModifier = prevBlockStake.StakeModifier; //pindexPrev.Header.BlockStake.StakeModifier;
			uint256 bnStakeModifierV2 = prevBlockStake.StakeModifierV2; //pindexPrev.Header.BlockStake.StakeModifierV2;
			int nStakeModifierHeight = pindexPrev.Height;
			var nStakeModifierTime = pindexPrev.Header.Time;

			// Calculate hash
			using (var ms = new MemoryStream())
			{
				var serializer = new BitcoinStream(ms, true);
				if (IsProtocolV3((int)nTimeTx))
				{
					serializer.ReadWrite(bnStakeModifierV2);
				}
				else
				{
					serializer.ReadWrite(nStakeModifier);
					serializer.ReadWrite(nTimeBlockFrom);
				}

				serializer.ReadWrite(txPrev.Time);
				serializer.ReadWrite(prevout.Hash);
				serializer.ReadWrite(prevout.N);
				serializer.ReadWrite(nTimeTx);

				context.Stake.HashProofOfStake = Hashes.Hash256(ms.ToArray());
			}

			//LogPrintf("CheckStakeKernelHash() : using modifier 0x%016x at height=%d timestamp=%s for block from timestamp=%s\n",
			//	nStakeModifier, nStakeModifierHeight,
			//	DateTimeStrFormat(nStakeModifierTime),

			//	DateTimeStrFormat(nTimeBlockFrom));

			//LogPrintf("CheckStakeKernelHash() : check modifier=0x%016x nTimeBlockFrom=%u nTimeTxPrev=%u nPrevout=%u nTimeTx=%u hashProof=%s\n",
			//	nStakeModifier,
			//	nTimeBlockFrom, txPrev.nTime, prevout.n, nTimeTx,
			//	hashProofOfStake.ToString());

			// Now check if proof-of-stake hash meets target protocol
			var hashProofOfStakeTarget = new BigInteger(1, context.Stake.HashProofOfStake.ToBytes(false));
			if (hashProofOfStakeTarget.CompareTo(bnTarget) > 0)
				ConsensusErrors.StakeHashInvalidTarget.Throw();

			//  if (fDebug && !fPrintProofOfStake)
			//  {
			//		LogPrintf("CheckStakeKernelHash() : using modifier 0x%016x at height=%d timestamp=%s for block from timestamp=%s\n",
			//		nStakeModifier, nStakeModifierHeight,
			//		DateTimeStrFormat(nStakeModifierTime),

			//		DateTimeStrFormat(nTimeBlockFrom));

			//		LogPrintf("CheckStakeKernelHash() : pass modifier=0x%016x nTimeBlockFrom=%u nTimeTxPrev=%u nPrevout=%u nTimeTx=%u hashProof=%s\n",
			//		nStakeModifier,
			//		nTimeBlockFrom, txPrev.nTime, prevout.n, nTimeTx,
			//		hashProofOfStake.ToString());
			//  }
		}

		public class StakeModifierContext
		{
			public ulong StakeModifier;
			public bool GeneratedStakeModifier;
			public long ModifierTime;
		}

		public void ComputeStakeModifier(ChainBase chainIndex, ChainedBlock pindex, BlockStake blockStake)
		{
			var pindexPrev = pindex.Previous;
			var blockStakePrev = pindexPrev == null ? null : this.stakeChain.Get(pindexPrev.HashBlock);

			// compute stake modifier
			var stakeContext = new StakeModifierContext();
			this.ComputeNextStakeModifier(chainIndex, pindexPrev, stakeContext);

			blockStake.SetStakeModifier(stakeContext.StakeModifier, stakeContext.GeneratedStakeModifier);
			blockStake.StakeModifierV2 = this.ComputeStakeModifierV2(
				pindexPrev, blockStakePrev, blockStake.IsProofOfWork() ? pindex.HashBlock : blockStake.PrevoutStake.Hash);
		}

		// Stake Modifier (hash modifier of proof-of-stake):
		// The purpose of stake modifier is to prevent a txout (coin) owner from
		// computing future proof-of-stake generated by this txout at the time
		// of transaction confirmation. To meet kernel protocol, the txout
		// must hash with a future stake modifier to generate the proof.
		// Stake modifier consists of bits each of which is contributed from a
		// selected block of a given block group in the past.
		// The selection of a block is based on a hash of the block's proof-hash and
		// the previous stake modifier.
		// Stake modifier is recomputed at a fixed time interval instead of every 
		// block. This is to make it difficult for an attacker to gain control of
		// additional bits in the stake modifier, even after generating a chain of
		// blocks.
		public void ComputeNextStakeModifier(ChainBase chainIndex, ChainedBlock pindexPrev, StakeValidator.StakeModifierContext stakeModifier)
		{
			stakeModifier.StakeModifier = 0;
			stakeModifier.GeneratedStakeModifier = false;
			if (pindexPrev == null)
			{
				stakeModifier.GeneratedStakeModifier = true;
				return; // genesis block's modifier is 0
			}

			// First find current stake modifier and its generation block time
			// if it's not old enough, return the same stake modifier
			long nModifierTime = 0;
			if (!this.GetLastStakeModifier(pindexPrev, stakeModifier))
				ConsensusErrors.ModifierNotFound.Throw();

			if (nModifierTime / this.consensusOptions.StakeModifierInterval >= pindexPrev.Header.Time / this.consensusOptions.StakeModifierInterval)
				return;

			// Sort candidate blocks by timestamp
			var sortedByTimestamp = new SortedDictionary<uint, ChainedBlock>();
			long nSelectionInterval = GetStakeModifierSelectionInterval();
			long nSelectionIntervalStart = (pindexPrev.Header.Time / this.consensusOptions.StakeModifierInterval) * this.consensusOptions.StakeModifierInterval - nSelectionInterval;
			var pindex = pindexPrev;
			while (pindex != null && pindex.Header.Time >= nSelectionIntervalStart)
			{
				sortedByTimestamp.Add(pindex.Header.Time, pindex);
				pindex = pindex.Previous;
			}
			//int nHeightFirstCandidate = pindex?.Height + 1 ?? 0;

			// Select 64 blocks from candidate blocks to generate stake modifier
			ulong nStakeModifierNew = 0;
			long nSelectionIntervalStop = nSelectionIntervalStart;
			var mapSelectedBlocks = new Dictionary<uint256, ChainedBlock>();
			var counter = sortedByTimestamp.Count;
			var sorted = sortedByTimestamp.Values.ToArray();
			for (int nRound = 0; nRound < Math.Min(64, counter); nRound++)
			{
				// add an interval section to the current selection round
				nSelectionIntervalStop += GetStakeModifierSelectionIntervalSection(nRound);

				// select a block from the candidates of current round
				BlockStake blockStake;
				if (!this.SelectBlockFromCandidates(sorted, mapSelectedBlocks, nSelectionIntervalStop, stakeModifier.StakeModifier, out pindex, out blockStake))
					ConsensusErrors.FailedSelectBlock.Throw();

				// write the entropy bit of the selected block
				nStakeModifierNew |= ((ulong)blockStake.GetStakeEntropyBit() << nRound);

				// add the selected block from candidates to selected list
				mapSelectedBlocks.Add(pindex.HashBlock, pindex);

				//LogPrint("stakemodifier", "ComputeNextStakeModifier: selected round %d stop=%s height=%d bit=%d\n", nRound, DateTimeStrFormat(nSelectionIntervalStop), pindex->nHeight, pindex->GetStakeEntropyBit());
			}

			//  // Print selection map for visualization of the selected blocks
			//  if (LogAcceptCategory("stakemodifier"))
			//  {
			//      string strSelectionMap = "";
			//      '-' indicates proof-of-work blocks not selected
			//      strSelectionMap.insert(0, pindexPrev->nHeight - nHeightFirstCandidate + 1, '-');
			//      pindex = pindexPrev;
			//      while (pindex && pindex->nHeight >= nHeightFirstCandidate)
			//      {
			//          // '=' indicates proof-of-stake blocks not selected
			//          if (pindex->IsProofOfStake())
			//              strSelectionMap.replace(pindex->nHeight - nHeightFirstCandidate, 1, "=");
			//          pindex = pindex->pprev;
			//      }

			//      BOOST_FOREACH(const PAIRTYPE(uint256, const CBlockIndex*)& item, mapSelectedBlocks)
			//      {
			//          // 'S' indicates selected proof-of-stake blocks
			//          // 'W' indicates selected proof-of-work blocks
			//          strSelectionMap.replace(item.second->nHeight - nHeightFirstCandidate, 1, item.second->IsProofOfStake()? "S" : "W");
			//      }

			//      LogPrintf("ComputeNextStakeModifier: selection height [%d, %d] map %s\n", nHeightFirstCandidate, pindexPrev->nHeight, strSelectionMap);
			//  }

			//LogPrint("stakemodifier", "ComputeNextStakeModifier: new modifier=0x%016x time=%s\n", nStakeModifierNew, DateTimeStrFormat(pindexPrev->GetBlockTime()));

			stakeModifier.StakeModifier = nStakeModifierNew;
			stakeModifier.GeneratedStakeModifier = true;			
		}

		// Stake Modifier (hash modifier of proof-of-stake):
		// The purpose of stake modifier is to prevent a txout (coin) owner from
		// computing future proof-of-stake generated by this txout at the time
		// of transaction confirmation. To meet kernel protocol, the txout
		// must hash with a future stake modifier to generate the proof.
		public uint256 ComputeStakeModifierV2(ChainedBlock pindexPrev, BlockStake blockStakePrev, uint256 kernel)
		{
			if (pindexPrev == null)
				return 0; // genesis block's modifier is 0

			uint256 stakeModifier;
			using (var ms = new MemoryStream())
			{
				var serializer = new BitcoinStream(ms, true);
				serializer.ReadWrite(kernel);
				serializer.ReadWrite(blockStakePrev.StakeModifierV2);
				stakeModifier = Hashes.Hash256(ms.ToArray());
			}

			return stakeModifier;
		}

		// Get the last stake modifier and its generation time from a given block
		private bool GetLastStakeModifier(ChainedBlock pindex, StakeValidator.StakeModifierContext stakeModifier)
		{
			stakeModifier.StakeModifier = 0;
			stakeModifier.ModifierTime = 0;

			if (pindex == null)
				return false;

			var blockStake = this.stakeChain.Get(pindex.HashBlock);
			while (pindex != null && pindex.Previous != null && !blockStake.GeneratedStakeModifier())
			{
				pindex = pindex.Previous;
				blockStake = this.stakeChain.Get(pindex.HashBlock);
			}

			if (!blockStake.GeneratedStakeModifier())
				return false; // error("GetLastStakeModifier: no generation at genesis block");

			stakeModifier.StakeModifier = blockStake.StakeModifier;
			stakeModifier.ModifierTime = pindex.Header.Time;

			return true;
		}

		// Get stake modifier selection interval (in seconds)
		private long GetStakeModifierSelectionInterval()
		{
			long nSelectionInterval = 0;
			for (int nSection = 0; nSection < 64; nSection++)
				nSelectionInterval += GetStakeModifierSelectionIntervalSection(nSection);
			return nSelectionInterval;
		}

		// MODIFIER_INTERVAL_RATIO:
		// ratio of group interval length between the last group and the first group
		const int MODIFIER_INTERVAL_RATIO = 3;
		// Get selection interval section (in seconds)
		private long GetStakeModifierSelectionIntervalSection(int nSection)
		{
			if (!(nSection >= 0 && nSection < 64))
				throw new ArgumentOutOfRangeException();
			return (this.consensusOptions.StakeModifierInterval * 63 / (63 + ((63 - nSection) * (MODIFIER_INTERVAL_RATIO - 1))));
		}

		// select a block from the candidate blocks in vSortedByTimestamp, excluding
		// already selected blocks in vSelectedBlocks, and with timestamp up to
		// nSelectionIntervalStop.
		private bool SelectBlockFromCandidates(ChainedBlock[] sortedByTimestamp,
			Dictionary<uint256, ChainedBlock> mapSelectedBlocks,
			long nSelectionIntervalStop, ulong nStakeModifierPrev, out ChainedBlock pindexSelected, out BlockStake blockStakeSelected)
		{

			bool fSelected = false;
			uint256 hashBest = 0;
			pindexSelected = null;
			blockStakeSelected = null;

			for (int i = 0; i < sortedByTimestamp.Length; i++)
			{
				var pindex = sortedByTimestamp[i];

				if (fSelected && pindex.Header.Time > nSelectionIntervalStop)
					break;

				if (mapSelectedBlocks.ContainsKey(pindex.HashBlock))
					continue;

				var blockStake = this.stakeChain.Get(pindex.HashBlock);

				// compute the selection hash by hashing its proof-hash and the
				// previous proof-of-stake modifier
				uint256 hashSelection;
				using (var ms = new MemoryStream())
				{
					var serializer = new BitcoinStream(ms, true);
					serializer.ReadWrite(blockStake.HashProof);
					serializer.ReadWrite(nStakeModifierPrev);

					hashSelection = Hashes.Hash256(ms.ToArray());
				}

				// the selection hash is divided by 2**32 so that proof-of-stake block
				// is always favored over proof-of-work block. this is to preserve
				// the energy efficiency property
				if (blockStake.IsProofOfStake())
					hashSelection >>= 32;

				if (fSelected && hashSelection < hashBest)
				{
					hashBest = hashSelection;
					pindexSelected = pindex;
					blockStakeSelected = blockStake;
				}
				else if (!fSelected)
				{
					fSelected = true;
					hashBest = hashSelection;
					pindexSelected = pindex;
					blockStakeSelected = blockStake;
				}
			}

			//LogPrint("stakemodifier", "SelectBlockFromCandidates: selection hash=%s\n", hashBest.ToString());
			return fSelected;
		}


		// ppcoin: total coin age spent in transaction, in the unit of coin-days.
		// Only those coins meeting minimum age requirement counts. As those
		// transactions not in main chain are not currently indexed so we
		// might not find out about their coin age. Older transactions are 
		// guaranteed to be in main chain by sync-checkpoint. This rule is
		// introduced to help nodes establish a consistent view of the coin
		// age (trust score) of competing branches.
		public bool GetCoinAge(ConcurrentChain chain, CoinView coinView,
			Transaction trx, ChainedBlock pindexPrev, out ulong nCoinAge)
		{

			BigInteger bnCentSecond = BigInteger.Zero;  // coin age in the unit of cent-seconds
			nCoinAge = 0;

			if (trx.IsCoinBase)
				return true;

			foreach (var txin in trx.Inputs)
			{
				var coins = coinView.FetchCoinsAsync(new[] { txin.PrevOut.Hash }).GetAwaiter().GetResult();
				if (coins == null || coins.UnspentOutputs.Length != 1)
					continue;

				var prevBlock = chain.GetBlock(coins.BlockHash);
				var prevUtxo = coins.UnspentOutputs[0];

				// First try finding the previous transaction in database
				//Transaction txPrev = trasnactionStore.Get(txin.PrevOut.Hash);
				//if (txPrev == null)
				//	continue;  // previous transaction not in main chain
				if (trx.Time < prevUtxo.Time)
					return false;  // Transaction timestamp violation

				if (IsProtocolV3((int)trx.Time))
				{
					if (IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
					{
						//LogPrint("coinage", "coin age skip nSpendDepth=%d\n", nSpendDepth + 1);
						continue; // only count coins meeting min confirmations requirement
					}
				}
				else
				{
					// Read block header
					//var block = blockStore.GetBlock(txPrev.GetHash());
					//if (block == null)
					//	return false; // unable to read block of previous transaction
					if (prevBlock.Header.Time + this.consensusOptions.StakeMinAge > trx.Time)
						continue; // only count coins meeting min age requirement
				}

				long nValueIn = prevUtxo._Outputs[txin.PrevOut.N].Value;
				var multiplier = BigInteger.ValueOf((trx.Time - prevUtxo.Time) / Money.CENT);
				bnCentSecond = bnCentSecond.Add(BigInteger.ValueOf(nValueIn).Multiply(multiplier));
				//bnCentSecond += new BigInteger(nValueIn) * (trx.Time - txPrev.Time) / CENT;


				//LogPrint("coinage", "coin age nValueIn=%d nTimeDiff=%d bnCentSecond=%s\n", nValueIn, nTime - txPrev.nTime, bnCentSecond.ToString());
			}

			BigInteger bnCoinDay = bnCentSecond.Multiply(BigInteger.ValueOf(Money.CENT / Money.COIN / (24 * 60 * 60)));
			//BigInteger bnCoinDay = bnCentSecond * CENT / COIN / (24 * 60 * 60);

			//LogPrint("coinage", "coin age bnCoinDay=%s\n", bnCoinDay.ToString());
			nCoinAge = new Target(bnCoinDay).ToCompact();

			return true;
		}

		public void CheckKernel(ContextInformation context, ChainedBlock pindexPrev, uint nBits, long nTime, OutPoint prevout, ref long pBlockTime)
		{
			var coins = this.coinView.FetchCoinsAsync(new[] { prevout.Hash }).GetAwaiter().GetResult();
			if (coins == null || coins.UnspentOutputs.Length != 1)
				ConsensusErrors.ReadTxPrevFailed.Throw();

			var prevBlock = chain.GetBlock(coins.BlockHash);
			var prevUtxo = coins.UnspentOutputs[0];

			//var txPrev = trasnactionStore.Get(prevout.Hash);
			//if (txPrev == null)
			//	return false;

			//// Read block header
			//var blockHashPrev = mapStore.GetBlockHash(prevout.Hash);
			//var block = blockHashPrev == null ? null : blockStore.GetBlock(blockHashPrev);
			//if (block == null)
			//	return false;

			if (IsProtocolV3((int)nTime))
			{
				if (IsConfirmedInNPrevBlocks(prevUtxo, pindexPrev, this.consensusOptions.StakeMinConfirmations - 1))
					ConsensusErrors.InvalidStakeDepth.Throw();
			}
			else
			{
				var nTimeBlockFrom = prevBlock.Header.Time;
				if (nTimeBlockFrom + this.consensusOptions.StakeMinAge > nTime)
					ConsensusErrors.MinAgeViolation.Throw();
			}

			var prevBlockStake = stakeChain.Get(pindexPrev.HashBlock);
			if (prevBlockStake == null)
				ConsensusErrors.BadStakeBlock.Throw();

			//if (pBlockTime)
				pBlockTime = prevBlock.Header.Time;

			this.CheckStakeKernelHash(context, pindexPrev, nBits, prevBlock, prevUtxo, prevBlockStake, prevout, (uint)nTime);
		}

		public static bool IsProtocolV2(int height)
		{
			return height > 0;
		}

		public static bool IsProtocolV3(int nTime)
		{
			return nTime > 1470467000;
		}

		public static ChainedBlock GetLastBlockIndex(StakeChain stakeChain, ChainedBlock index, bool proofOfStake)
		{
			if (index == null)
				throw new ArgumentNullException(nameof(index));
			var blockStake = stakeChain.Get(index.HashBlock);

			while (index.Previous != null && (blockStake.IsProofOfStake() != proofOfStake))
			{
				index = index.Previous;
				blockStake = stakeChain.Get(index.HashBlock);
			}

			return index;
		}

		public static Target GetNextTargetRequired(StakeChain stakeChain, ChainedBlock indexLast, NBitcoin.Consensus consensus, bool proofOfStake)
		{
			// Genesis block
			if (indexLast == null)
				return consensus.PowLimit;

			// find the last two blocks that correspond to the mining algo 
			// (i.e if this is a POS block we need to find the last two POS blocks)
			var targetLimit = proofOfStake
				? GetProofOfStakeLimit(consensus, indexLast.Height)
				: consensus.PowLimit.ToBigInteger();

			// first block
			var pindexPrev = GetLastBlockIndex(stakeChain, indexLast, proofOfStake);
			if (pindexPrev.Previous == null)
				return new Target(targetLimit);

			// second block
			var pindexPrevPrev = GetLastBlockIndex(stakeChain, pindexPrev.Previous, proofOfStake);
			if (pindexPrevPrev.Previous == null)
				return new Target(targetLimit);


			int targetSpacing = GetTargetSpacing(indexLast.Height);
			int actualSpacing = (int)(pindexPrev.Header.Time - pindexPrevPrev.Header.Time);
			if (IsProtocolV1RetargetingFixed(indexLast.Height))
			{
				if (actualSpacing < 0) actualSpacing = targetSpacing;
			}
			if (IsProtocolV3((int)indexLast.Header.Time))
			{
				if (actualSpacing > targetSpacing * 10) actualSpacing = targetSpacing * 10;
			}

			// target change every block
			// retarget with exponential moving toward target spacing
			var targetTimespan = 16 * 60; // 16 mins
			var target = pindexPrev.Header.Bits.ToBigInteger();

			int interval = targetTimespan / targetSpacing;
			target = target.Multiply(BigInteger.ValueOf(((interval - 1) * targetSpacing + actualSpacing + actualSpacing)));
			target = target.Divide(BigInteger.ValueOf(((interval + 1) * targetSpacing)));

			if (target.CompareTo(BigInteger.Zero) <= 0 || target.CompareTo(targetLimit) >= 1)
				//if (target <= 0 || target > targetLimit)
				target = targetLimit;

			return new Target(target);
		}

		private static BigInteger GetProofOfStakeLimit(NBitcoin.Consensus consensus, int height)
		{
			return IsProtocolV2(height) ? consensus.ProofOfStakeLimitV2 : consensus.ProofOfStakeLimit;
		}

		public static int GetTargetSpacing(int height)
		{
			return IsProtocolV2(height) ? 64 : 60;
		}

		private static bool IsProtocolV1RetargetingFixed(int height)
		{
			return height > 0;
		}

		public static uint GetPastTimeLimit(ChainedBlock chainedBlock)
		{
			if (IsProtocolV2(chainedBlock.Height))
				return chainedBlock.Header.Time;
			else
				return GetMedianTimePast(chainedBlock);
		}

		private const int MedianTimeSpan = 11;

		public static uint GetMedianTimePast(ChainedBlock chainedBlock)
		{
			var soretedList = new SortedSet<uint>();
			var pindex = chainedBlock;
			for (int i = 0; i < MedianTimeSpan && pindex != null; i++, pindex = pindex.Previous)
				soretedList.Add(pindex.Header.Time);

			return (soretedList.First() - soretedList.Last()) / 2;
		}
	}

}
