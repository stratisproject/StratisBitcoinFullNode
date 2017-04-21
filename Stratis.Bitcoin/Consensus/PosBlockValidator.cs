using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Consensus
{
	public class PosBlockValidator
	{
		private const int MAX_BLOCK_SIZE = 1000000;

		public const long COIN = 100000000;
		public const long CENT = 1000000;

		private static bool IsProtocolV1RetargetingFixed(int height)
		{
			return height > 0;
		}

		public static bool IsProtocolV2(int height)
		{
			return height > 0;
		}

		public static bool IsProtocolV3(int nTime)
		{
			return nTime > 1470467000;
		}

		private static BigInteger GetProofOfStakeLimit(NBitcoin.Consensus consensus, int height)
		{
			return IsProtocolV2(height) ? consensus.ProofOfStakeLimitV2 : consensus.ProofOfStakeLimit;
		}

		public static int GetTargetSpacing(int height)
		{
			return IsProtocolV2(height) ? 64 : 60;
		}


		// Get time weight
		//public static long GetWeight(long nIntervalBeginning, long nIntervalEnd)
		//{
		//	// Kernel hash weight starts from 0 at the min age
		//	// this change increases active coins participating the hash and helps
		//	// to secure the network when proof-of-stake difficulty is low

		//	return nIntervalEnd - nIntervalBeginning - StakeMinAge;
		//}

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

		// find last block index up to index
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


		public static bool IsCanonicalBlockSignature(Block block, bool checkLowS)
		{
			if (BlockStake.IsProofOfWork(block))
			{
				return block.BlockSignatur.IsEmpty();
			}

			return checkLowS ?
				ScriptEvaluationContext.IsLowDerSignature(block.BlockSignatur.Signature) :
				ScriptEvaluationContext.IsValidSignatureEncoding(block.BlockSignatur.Signature);
		}

		public static bool EnsureLowS(BlockSignature blockSignature)
		{
			var signature = new ECDSASignature(blockSignature.Signature);
			if (!signature.IsLowS)
				blockSignature.Signature = signature.MakeCanonical().ToDER();
			return true;
		}
	}

}
