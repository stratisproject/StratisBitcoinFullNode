using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Miner
{
	public class ReserveScript
	{
		public Script reserveSfullNodecript { get; set; }
	}

	public class PowMining
	{
		// Default for -blockmintxfee, which sets the minimum feerate for a transaction in blocks created by mining code 
		public const int DefaultBlockMinTxFee = 1000;
		// Default for -blockmaxsize, which controls the maximum size of block the mining code will create 
		public const int DefaultBlockMaxSize = 750000;
		// Default for -blockmaxweight, which controls the range of block weights the mining code will create 
		public const int DefaultBlockMaxWeight = 3000000;

		const int InnerLoopCount = 0x10000;

		private readonly ConsensusLoop consensusLoop;
		private readonly ConcurrentChain chain;
		private readonly Network network;
		private readonly IDateTimeProvider dateTimeProvider;
		private readonly AssemblerFactory blockAssemblerFactory;
		private readonly BlockRepository blockRepository;
		private readonly ChainBehavior.ChainState chainState;
		private readonly Signals signals;
		private readonly FullNode.CancellationProvider cancellationProvider;
		private uint256 hashPrevBlock;
		private Task mining;

		public PowMining(ConsensusLoop consensusLoop, ConcurrentChain chain, Network network,
			IDateTimeProvider dateTimeProvider, AssemblerFactory blockAssemblerFactory, BlockRepository blockRepository,
			BlockStore.ChainBehavior.ChainState chainState, Signals signals, FullNode.CancellationProvider cancellationProvider)
		{
			this.consensusLoop = consensusLoop;
			this.chain = chain;
			this.network = network;
			this.dateTimeProvider = dateTimeProvider;
			this.blockAssemblerFactory = blockAssemblerFactory;
			this.blockRepository = blockRepository;
			this.chainState = chainState;
			this.signals = signals;
			this.cancellationProvider = cancellationProvider;
		}

		public Task Mine(Script reserveScript)
		{
			if (this.mining != null)
				return this.mining; // already mining

			this.mining = AsyncLoop.Run("PowMining.Mine", token =>
				{
					this.GenerateBlocks(new ReserveScript {reserveSfullNodecript = reserveScript}, int.MaxValue, int.MaxValue);
					this.mining = null;
					return Task.CompletedTask;
				},
				cancellationProvider.Cancellation.Token,
				repeatEvery: TimeSpans.RunOnce,
				startAfter: TimeSpans.TenSeconds);

			return this.mining;
		}

		public List<uint256> GenerateBlocks(ReserveScript reserveScript, ulong generate, ulong maxTries)
		{
			ulong nHeightStart = 0;
			ulong nHeightEnd = 0;
			ulong nHeight = 0;

			nHeightStart = (ulong) this.chain.Height;
			nHeight = nHeightStart;
			nHeightEnd = nHeightStart + generate;
			int nExtraNonce = 0;
			var blocks = new List<uint256>();


			// BlockTemplate pblocktemplate = null;

			while (nHeight < nHeightEnd)
			{
				try
				{
					if (this.chain.Tip != this.consensusLoop.Tip)
					{
						this.cancellationProvider.Cancellation.Token.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
						continue;
					}

					var pblocktemplate = this.blockAssemblerFactory.Create().CreateNewBlock(reserveScript.reserveSfullNodecript);

					this.IncrementExtraNonce(pblocktemplate.Block, this.chain.Tip, nExtraNonce);
					var pblock = pblocktemplate.Block;

					while (maxTries > 0 && pblock.Header.Nonce < InnerLoopCount && !pblock.CheckProofOfWork())
					{
						++pblock.Header.Nonce;
						--maxTries;
					}

					if (maxTries == 0)
						break;

					if (pblock.Header.Nonce == InnerLoopCount)
						continue;

					var newChain = new ChainedBlock(pblock.Header, pblock.GetHash(), this.chain.Tip);

					if (newChain.ChainWork <= this.chain.Tip.ChainWork)
						continue;

					this.chain.SetTip(newChain);

					var blockResult = new BlockResult {Block = pblock};
					this.consensusLoop.AcceptBlock(new ContextInformation(blockResult, this.network.Consensus));

					if (blockResult.ChainedBlock == null)
						break; //reorg

					if (blockResult.Error != null)
						return blocks;

					// similar logic to what's in the full node code
					this.chainState.HighestValidatedPoW = this.consensusLoop.Tip;
					this.signals.Blocks.Broadcast(pblock);

					Logs.Mining.LogInformation($"Mined new {(BlockStake.IsProofOfStake(blockResult.Block) ? "POS" : "POW")} block: {blockResult.ChainedBlock.HashBlock}");

					++nHeight;
					blocks.Add(pblock.GetHash());

					// ensure the block is written to disk
					ulong retry = 0;
					while (++retry < maxTries &&
					       !this.blockRepository.ExistAsync(blockResult.ChainedBlock.HashBlock).GetAwaiter().GetResult())
						Thread.Sleep(100);

					pblocktemplate = null;
				}
				catch (ConsensusErrorException cer)
				{
					if (cer.ConsensusError == ConsensusErrors.InvalidPrevTip)
						continue;

					throw;
				}
			}

			return blocks;
	    }

		public void IncrementExtraNonce(Block pblock, ChainedBlock pindexPrev, int nExtraNonce)
		{
			// Update nExtraNonce
			if (hashPrevBlock != pblock.Header.HashPrevBlock)
			{
				nExtraNonce = 0;
				hashPrevBlock = pblock.Header.HashPrevBlock;
			}
			++nExtraNonce;
			int nHeight = pindexPrev.Height + 1; // Height first in coinbase required for block.version=2
			var txCoinbase = pblock.Transactions[0];
			txCoinbase.Inputs[0] = TxIn.CreateCoinbase(nHeight);

			Guard.Assert(txCoinbase.Inputs[0].ScriptSig.Length <= 100);
			pblock.UpdateMerkleRoot();
		}

		public static Target GetWorkRequired(NBitcoin.Consensus consensus, ChainedBlock chainedBlock)
		{
			// Genesis block
			if (chainedBlock.Height == 0)
				return consensus.PowLimit;
			var nProofOfWorkLimit = consensus.PowLimit;
			var pindexLast = chainedBlock.Previous;
			var height = chainedBlock.Height;

			if (pindexLast == null)
				return nProofOfWorkLimit;

			// Only change once per interval
			if ((height) % consensus.DifficultyAdjustmentInterval != 0)
			{
				if (consensus.PowAllowMinDifficultyBlocks)
				{
					// Special difficulty rule for testnet:
					// If the new block's timestamp is more than 2* 10 minutes
					// then allow mining of a min-difficulty block.
					if (chainedBlock.Header.BlockTime > pindexLast.Header.BlockTime + TimeSpan.FromTicks(consensus.PowTargetSpacing.Ticks * 2))
						return nProofOfWorkLimit;
					else
					{
						// Return the last non-special-min-difficulty-rules-block
						ChainedBlock pindex = pindexLast;
						while (pindex.Previous != null && (pindex.Height % consensus.DifficultyAdjustmentInterval) != 0 && pindex.Header.Bits == nProofOfWorkLimit)
							pindex = pindex.Previous;
						return pindex.Header.Bits;
					}
				}
				return pindexLast.Header.Bits;
			}

			// Go back by what we want to be 14 days worth of blocks
			var pastHeight = pindexLast.Height - (consensus.DifficultyAdjustmentInterval - 1);
			ChainedBlock pindexFirst = chainedBlock.EnumerateToGenesis().FirstOrDefault(o => o.Height == pastHeight);
			Guard.Assert(pindexFirst != null);

			if (consensus.PowNoRetargeting)
				return pindexLast.Header.Bits;

			// Limit adjustment step
			var nActualTimespan = pindexLast.Header.BlockTime - pindexFirst.Header.BlockTime;
			if (nActualTimespan < TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4))
				nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks / 4);
			if (nActualTimespan > TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4))
				nActualTimespan = TimeSpan.FromTicks(consensus.PowTargetTimespan.Ticks * 4);

			// Retarget
			var bnNew = pindexLast.Header.Bits.ToBigInteger();
			bnNew = bnNew.Multiply(BigInteger.ValueOf((long)nActualTimespan.TotalSeconds));
			bnNew = bnNew.Divide(BigInteger.ValueOf((long)consensus.PowTargetTimespan.TotalSeconds));
			var newTarget = new Target(bnNew);
			if (newTarget > nProofOfWorkLimit)
				newTarget = nProofOfWorkLimit;

			return newTarget;
		}
	}
}
