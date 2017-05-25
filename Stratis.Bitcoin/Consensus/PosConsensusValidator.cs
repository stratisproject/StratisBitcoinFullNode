using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class PosConsensusValidator : PowConsensusValidator
	{
		private readonly StakeValidator stakeValidator;
		private readonly StakeChain stakeChain;
		private readonly ConcurrentChain chain;
		private readonly CoinView coinView;
		private readonly PosConsensusOptions consensusOptions;

		public PosConsensusValidator(StakeValidator stakeValidator, Network network, 
			StakeChain stakeChain, ConcurrentChain chain, CoinView coinView) 
			: base(network)
		{
			Guard.NotNull(network.Consensus.Option<PosConsensusOptions>(), nameof(network.Consensus.Options));

			this.stakeValidator = stakeValidator;
			this.stakeChain = stakeChain;
			this.chain = chain;
			this.coinView = coinView;
			this.consensusOptions = network.Consensus.Option<PosConsensusOptions>();
		}

		public StakeValidator StakeValidator => this.stakeValidator;

		public override void CheckBlockReward(ContextInformation context, Money nFees, ChainedBlock chainedBlock, Block block)
		{
			if (BlockStake.IsProofOfStake(block))
			{
				// proof of stake invalidates previous inputs 
				// and spends the inputs to new outputs with the 
				// additional stake reward, next calculate the  
				// reward does not exceed the consensus rules  

				var stakeReward = block.Transactions[1].TotalOut - context.Stake.TotalCoinStakeValueIn;
				var calcStakeReward = nFees + GetProofOfStakeReward(chainedBlock.Height);

				if (stakeReward > calcStakeReward)
					ConsensusErrors.BadCoinstakeAmount.Throw();
			}
			else
			{
				var blockReward = nFees + GetProofOfWorkReward(chainedBlock.Height);
				if (block.Transactions[0].TotalOut > blockReward)
					ConsensusErrors.BadCoinbaseAmount.Throw();
			}
		}

		public override void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler)
		{
			// compute and store the stake proofs
			this.CheckAndComputeStake(context);

			base.ExecuteBlock(context, taskScheduler);

			// TODO: a temporary fix til this methods is fixed in NStratis
			(this.stakeChain as StakeChainStore).Set(context.BlockResult.ChainedBlock, context.Stake.BlockStake);
		}

		public override void CheckBlock(ContextInformation context)
		{
			base.CheckBlock(context);

			var block = context.BlockResult.Block;

			// Check timestamp
			if (block.Header.Time > FutureDriftV2(DateTime.UtcNow.Ticks))
				ConsensusErrors.BlockTimestampTooFar.Throw();

			if (BlockStake.IsProofOfStake(block))
			{
				// Coinbase output should be empty if proof-of-stake block
				if (block.Transactions[0].Outputs.Count != 1 || !block.Transactions[0].Outputs[0].IsEmpty)
					ConsensusErrors.BadStakeBlock.Throw();

				// Second transaction must be coinstake, the rest must not be
				if (!block.Transactions[1].IsCoinStake)
					ConsensusErrors.BadStakeBlock.Throw();

				if (block.Transactions.Skip(2).Any(t => t.IsCoinStake))
					ConsensusErrors.BadMultipleCoinstake.Throw();
			}

			// Check proof-of-stake block signature
			if (!CheckBlockSignature(block))
				ConsensusErrors.BadBlockSignature.Throw();

			// Check transactions
			foreach (var transaction in block.Transactions)
			{
				// check transaction timestamp
				if (block.Header.Time < transaction.Time)
					ConsensusErrors.BlockTimeBeforeTrx.Throw();
			}
		}

		public override void ContextualCheckBlock(ContextInformation context)
		{
			base.ContextualCheckBlock(context);

			 // TODO: fix this validation code

			//// check proof-of-stake
			//// Limited duplicity on stake: prevents block flood attack
			//// Duplicate stake allowed only when there is orphan child block
			//if (!fReindex && !fImporting && pblock->IsProofOfStake() && setStakeSeen.count(pblock->GetProofOfStake()) && !mapOrphanBlocksByPrev.count(hash))
			//	return error("ProcessBlock() : duplicate proof-of-stake (%s, %d) for block %s", pblock->GetProofOfStake().first.ToString(), pblock->GetProofOfStake().second, hash.ToString());

			//if (!BlockValidator.IsCanonicalBlockSignature(context.BlockResult.Block, false))
			//{
			//	//if (node != null && (int)node.Version >= CANONICAL_BLOCK_SIG_VERSION)
			//	//node.Misbehaving(100);

			//	//return false; //error("ProcessBlock(): bad block signature encoding");
			//}

			//if (!BlockValidator.IsCanonicalBlockSignature(context.BlockResult.Block, true))
			//{
			//	//if (pfrom && pfrom->nVersion >= CANONICAL_BLOCK_SIG_LOW_S_VERSION)
			//	//{
			//	//	pfrom->Misbehaving(100);
			//	//	return error("ProcessBlock(): bad block signature encoding (low-s)");
			//	//}

			//	if (!BlockValidator.EnsureLowS(context.BlockResult.Block.BlockSignatur))
			//		return false; // error("ProcessBlock(): EnsureLowS failed");
			//}

		}

		public override void ContextualCheckBlockHeader(ContextInformation context)
		{
			base.ContextualCheckBlockHeader(context);

			var chainedBlock = context.BlockResult.ChainedBlock;

			if (!StakeValidator.IsProtocolV3((int)chainedBlock.Header.Time))
			{
				if (chainedBlock.Header.Version > BlockHeader.CURRENT_VERSION)
					ConsensusErrors.BadVersion.Throw();
			}

			if (StakeValidator.IsProtocolV2(chainedBlock.Height) && chainedBlock.Header.Version < 7)
				ConsensusErrors.BadVersion.Throw(); 
			else if (!StakeValidator.IsProtocolV2(chainedBlock.Height) && chainedBlock.Header.Version > 6)
				ConsensusErrors.BadVersion.Throw();

			if (context.Stake.BlockStake.IsProofOfWork() && chainedBlock.Height > this.ConsensusParams.LastPOWBlock)
				ConsensusErrors.ProofOfWorkTooHeigh.Throw();

			// Check coinbase timestamp
			if (chainedBlock.Header.Time > FutureDrift(context.BlockResult.Block.Transactions[0].Time, chainedBlock.Height))
				ConsensusErrors.TimeTooNew.Throw();

			// Check coinstake timestamp
			if (context.Stake.BlockStake.IsProofOfStake() 
				&& !PosConsensusValidator.CheckCoinStakeTimestamp(chainedBlock.Height, chainedBlock.Header.Time, context.BlockResult.Block.Transactions[1].Time))
				ConsensusErrors.StakeTimeViolation.Throw();

			// Check timestamp against prev
			if (chainedBlock.Header.Time <= StakeValidator.GetPastTimeLimit(chainedBlock.Previous) 
				|| FutureDrift(chainedBlock.Header.Time, chainedBlock.Height) < chainedBlock.Previous.Header.Time)
				ConsensusErrors.BlockTimestampTooEarly.Throw();

		}

		public const uint STAKE_TIMESTAMP_MASK = 15;
		// Check whether the coinstake timestamp meets protocol
		public static bool CheckCoinStakeTimestamp(int nHeight, long nTimeBlock, long nTimeTx)
		{
			if (StakeValidator.IsProtocolV2(nHeight))
				return (nTimeBlock == nTimeTx) && ((nTimeTx & STAKE_TIMESTAMP_MASK) == 0);
			else
				return (nTimeBlock == nTimeTx);
		}

		private static long FutureDriftV1(long nTime) { return nTime + 10 * 60; }
		private static long FutureDriftV2(long nTime) { return nTime + 128 * 60 * 60; }
		private static long FutureDrift(long nTime, int nHeight) { return StakeValidator.IsProtocolV2(nHeight) ? FutureDriftV2(nTime) : FutureDriftV1(nTime); }

		public static bool CheckBlockSignature(Block block)
		{
			if (BlockStake.IsProofOfWork(block))
				return block.BlockSignatur.IsEmpty();

			if (block.BlockSignatur.IsEmpty())
				return false;

			var txout = block.Transactions[1].Outputs[1];

			if (PayToPubkeyTemplate.Instance.CheckScriptPubKey(txout.ScriptPubKey))
			{
				var pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(txout.ScriptPubKey);
				return pubKey.Verify(block.GetHash(), new ECDSASignature(block.BlockSignatur.Signature));
			}

			if (StakeValidator.IsProtocolV3((int)block.Header.Time))
			{
				// Block signing key also can be encoded in the nonspendable output
				// This allows to not pollute UTXO set with useless outputs e.g. in case of multisig staking

				var ops = txout.ScriptPubKey.ToOps().ToList();
				if (!ops.Any()) // script.GetOp(pc, opcode, vchPushValue))
					return false;
				if (ops.ElementAt(0).Code != OpcodeType.OP_RETURN) // OP_RETURN)
					return false;
				if (ops.Count < 2) // script.GetOp(pc, opcode, vchPushValue)
					return false;
				var data = ops.ElementAt(1).PushData;
				if (!ScriptEvaluationContext.IsCompressedOrUncompressedPubKey(data))
					return false;
				return new PubKey(data).Verify(block.GetHash(), new ECDSASignature(block.BlockSignatur.Signature));
			}

			return false;
		}

		public override void CheckBlockHeader(ContextInformation context)
		{
			context.SetStake();

			if (context.Stake.BlockStake.IsProofOfWork())
			{
				if (context.CheckPow && !context.BlockResult.Block.Header.CheckProofOfWork())
					ConsensusErrors.HighHash.Throw();
			}

			context.NextWorkRequired  = StakeValidator.GetNextTargetRequired(stakeChain, context.BlockResult.ChainedBlock.Previous, context.Consensus,
				context.Stake.BlockStake.IsProofOfStake());
		}

		public void CheckAndComputeStake(ContextInformation context)
		{
			var pindex = context.BlockResult.ChainedBlock;
			var block = context.BlockResult.Block;
			var blockStake = context.Stake.BlockStake;

			// Verify hash target and signature of coinstake tx
			if (BlockStake.IsProofOfStake(block))
			{
				var pindexPrev = pindex.Previous;

				var prevBlockStake = this.stakeChain.Get(pindexPrev.HashBlock);
				if (prevBlockStake == null)
					ConsensusErrors.PrevStakeNull.Throw();

				this.stakeValidator.CheckProofOfStake(context, pindexPrev, prevBlockStake, block.Transactions[1], pindex.Header.Bits.ToCompact());
			}

			// PoW is checked in CheckBlock()
			if (BlockStake.IsProofOfWork(block))
			{
				context.Stake.HashProofOfStake = pindex.Header.GetPoWHash();
			}

			// TODO: is this the same as chain work?
			// compute chain trust score
			//pindexNew.nChainTrust = (pindexNew->pprev ? pindexNew->pprev->nChainTrust : 0) + pindexNew->GetBlockTrust();

			// compute stake entropy bit for stake modifier
			if (!blockStake.SetStakeEntropyBit(blockStake.GetStakeEntropyBit()))
				ConsensusErrors.SetStakeEntropyBitFailed.Throw();

			// Record proof hash value
			blockStake.HashProof = context.Stake.HashProofOfStake;

			// compute stake modifier
			this.stakeValidator.ComputeStakeModifier(chain, pindex, blockStake);
		}

		public override Money GetProofOfWorkReward(int height)
		{
			if (this.IsPremine(height))
				return this.consensusOptions.PremineReward;

			return this.ConsensusOptions.ProofOfWorkReward;
		}

		// miner's coin stake reward
		public Money GetProofOfStakeReward(int height)
		{
			if (this.IsPremine(height))
				return this.consensusOptions.PremineReward;

			return this.consensusOptions.ProofOfStakeReward;
		}

		private bool IsPremine(int height)
		{
			return this.consensusOptions.PremineHeight > 0 &&
			       this.consensusOptions.PremineReward > 0 &&
				   height == this.consensusOptions.PremineHeight;
		}
	}
}
