using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Miner
{
	public class ReserveScript
	{
		public Script reserveSfullNodecript { get; set; }
	}

	public class Mining
    {
	    private readonly FullNode fullNode;
	    private readonly DateTimeProvider dateTimeProvider;

	    public Mining(FullNode fullNode, DateTimeProvider dateTimeProvider)
	    {
		    this.fullNode = fullNode;
		    this.dateTimeProvider = dateTimeProvider;
	    }

		public List<uint256> GenerateBlocks(ReserveScript reserveScript, int generate, int maxTries, bool keepScript)
	    {
			// temporary code to mine blocks while doing some simulations
			// this will be refactored to have the same logic as core with regards to 
			// selecting and sorting transactions from the mempool. 

		    if (fullNode.Chain.Tip != fullNode.ConsensusLoop.Tip)
			    return Enumerable.Empty<uint256>().ToList();

			List<Block> blocks = new List<Block>();

			for (int i = 0; i < generate; i++)
			{
				uint nonce = 0;
				Block block = new Block();
				block.Header.HashPrevBlock = fullNode.Chain.Tip.HashBlock;
				//block.Header.Bits = GetWorkRequired(fullNode.Network.Consensus,new ChainedBlock(block.Header, (uint256) null, fullNode.Chain.Tip));
				block.Header.GetWorkRequired(fullNode.Network, fullNode.Chain.Tip);
				block.Header.UpdateTime(dateTimeProvider.GetTimeOffset(), fullNode.Network, fullNode.Chain.Tip);
				var coinbase = new Transaction();
				coinbase.AddInput(TxIn.CreateCoinbase(fullNode.Chain.Height + 1));
				coinbase.AddOutput(new TxOut(fullNode.Network.GetReward(fullNode.Chain.Height + 1), reserveScript.reserveSfullNodecript));
				block.AddTransaction(coinbase);
				//if (passedTransactions?.Any() ?? false)
				//{
				//	passedTransactions = Reorder(passedTransactions);
				//	block.Transactions.AddRange(passedTransactions);
				//}
				block.UpdateMerkleRoot();
				var retry = 0;
			    while (!block.CheckProofOfWork() && !fullNode.IsDisposed && ++retry < maxTries)
			    {
			        block.Header.Nonce = ++nonce;
                    //Console.WriteLine("b: "+block.Header.GetHash());
                    //Console.WriteLine("t: " +block.Header.Bits.ToUInt256());
                }
				if (fullNode.IsDisposed || retry >= maxTries)
					return blocks.Select(b => b.GetHash()).ToList();
				blocks.Add(block);

				var newChain = new ChainedBlock(block.Header, block.GetHash(), fullNode.Chain.Tip);
				fullNode.Chain.SetTip(newChain);

				var blockResult = new BlockResult {Block = block};
				fullNode.ConsensusLoop.AcceptBlock(blockResult);

				// similar logic to what's in the full node code
				if (blockResult.Error == null)
				{
					fullNode.ChainBehaviorState.HighestValidatedPoW = fullNode.ConsensusLoop.Tip;
					//if (fullNode.Chain.Tip.HashBlock == blockResult.ChainedBlock.HashBlock)
					//{
					//	var unused = cache.FlushAsync();
					//}
					fullNode.Signals.Blocks.Broadcast(block);
				}

				// ensure the block is written to disk
				retry = 0;
				while (++retry < maxTries && this.fullNode.BlockStoreManager.BlockRepository.GetAsync(blockResult.ChainedBlock.HashBlock).GetAwaiter().GetResult() == null)
					Thread.Sleep(100);	
				if (retry >= maxTries)
					return blocks.Select(b => b.GetHash()).ToList();
			}

		    return blocks.Select(b => b.GetHash()).ToList();
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
			Check.Assert(pindexFirst != null);

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
