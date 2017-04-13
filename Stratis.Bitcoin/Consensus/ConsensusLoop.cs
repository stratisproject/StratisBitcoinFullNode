using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class BlockResult
	{
		public ChainedBlock ChainedBlock
		{
			get; set;
		}
		public Block Block
		{
			get; set;
		}
		public ConsensusError Error
		{
			get; set;
		}
	}
	public class ConsensusLoop
	{
		public ConsensusLoop(ConsensusValidator validator, ConcurrentChain chain, CoinView utxoSet, LookaheadBlockPuller puller, StakeChain stakeChain = null)
		{
			Guard.NotNull(validator, nameof(validator));
			Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(utxoSet, nameof(utxoSet));
			Guard.NotNull(puller, nameof(puller));
			
			this.Validator = validator;
			this.Chain = chain;
			this.UTXOSet = utxoSet;
			this.Puller = puller;

			// chain of stake info can be null if POS is not enabled
			this.StakeChain = stakeChain;
		}
		StopWatch watch = new StopWatch();

		public StakeChain StakeChain { get; }
		public LookaheadBlockPuller Puller { get; }
		public ConcurrentChain Chain { get; }
		public CoinView UTXOSet { get; }
		public ConsensusValidator Validator { get; }
		public ChainedBlock Tip { get; private set; }
		public ThresholdConditionCache BIP9 { get; private set; }

		public void Initialize()
		{
			var utxoHash = UTXOSet.GetBlockHashAsync().GetAwaiter().GetResult();
			while(true)
			{
				Tip = Chain.GetBlock(utxoHash);
				if(Tip != null)
					break;
				utxoHash = UTXOSet.Rewind().GetAwaiter().GetResult();
			}
			Puller.SetLocation(Tip);
			BIP9 = new ThresholdConditionCache(Validator.ConsensusParams);
		}

		public IEnumerable<BlockResult> Execute(CancellationToken cancellationToken)
		{
			while(true)
			{
				yield return ExecuteNextBlock(cancellationToken);
			}
		}

		public ConsensusFlags GetFlags(ChainedBlock block = null)
		{
			block = block ?? Tip;
			lock(this.BIP9)
			{
				var states = this.BIP9.GetStates(block.Previous);
				var flags = new ConsensusFlags(block, states, Validator.ConsensusParams);
				return flags;
			}
		}

		public BlockResult ExecuteNextBlock(CancellationToken cancellationToken)
		{
			BlockResult result = new BlockResult();
			try
			{
				using(watch.Start(o => Validator.PerformanceCounter.AddBlockFetchingTime(o)))
				{
					while(true)
					{
						result.Block = Puller.NextBlock(cancellationToken);
						if(result.Block != null)
							break;
						else
						{
							while(true)
							{
								var hash = UTXOSet.Rewind().GetAwaiter().GetResult();
								var rewinded = Chain.GetBlock(hash);
								if(rewinded == null)
									continue;
								Tip = rewinded;
								Puller.SetLocation(rewinded);
								break;
							}
						}
					}
				}

				this.AcceptBlock(result);
			}
			catch(ConsensusErrorException ex)
			{
				result.Error = ex.ConsensusError;
			}
			return result;
		}

		public void AcceptBlock(BlockResult result)
		{
			var context = new ContextInformation(result, this.Validator.ConsensusParams, this.Validator.ConsensusOptions);

			using (watch.Start(o => Validator.PerformanceCounter.AddBlockProcessingTime(o)))
			{
				if (result.Block.Header.HashPrevBlock != Tip.HashBlock)
					return; // reorg
				result.ChainedBlock = new ChainedBlock(result.Block.Header, result.Block.Header.GetHash(), Tip);
				//Liberate from memory the block created above if possible
				result.ChainedBlock = Chain.GetBlock(result.ChainedBlock.HashBlock) ?? result.ChainedBlock;
				context.SetChain(this.StakeChain);

				// validation flow
				Validator.CheckBlockHeader(context, this.StakeChain);
				Validator.ContextualCheckBlockHeader(context);
				context.Flags = GetFlags(result.ChainedBlock);
				Validator.ContextualCheckBlock(context);
				Validator.CheckBlock(context);
			}

			context.Set = new UnspentOutputSet();
			using (watch.Start(o => Validator.PerformanceCounter.AddUTXOFetchingTime(o)))
			{
				var ids = GetIdsToFetch(result.Block, context.Flags.EnforceBIP30);
				var coins = UTXOSet.FetchCoinsAsync(ids).GetAwaiter().GetResult();
				context.Set.SetCoins(coins);
			}

			TryPrefetchAsync(context.Flags);
			using (watch.Start(o => Validator.PerformanceCounter.AddBlockProcessingTime(o)))
			{
				Validator.ExecuteBlock(context, null, this.StakeChain);
			}

			UTXOSet.SaveChangesAsync(context.Set.GetCoins(UTXOSet), null, Tip.HashBlock, result.ChainedBlock.HashBlock);

			Tip = result.ChainedBlock;
		}

		private Task TryPrefetchAsync(ConsensusFlags flags)
		{
			Task prefetching = Task.FromResult<bool>(true);
			if(UTXOSet is CachedCoinView)
			{
				var nextBlock = this.Puller.TryGetLookahead(0);
				if(nextBlock != null)
					prefetching = UTXOSet.FetchCoinsAsync(GetIdsToFetch(nextBlock, flags.EnforceBIP30));
			}
			return prefetching;
		}
		public static uint256[] GetIdsToFetch(Block block, bool enforceBIP30)
		{
			HashSet<uint256> ids = new HashSet<uint256>();
			foreach(var tx in block.Transactions)
			{
				if(enforceBIP30)
				{
					var txId = tx.GetHash();
					ids.Add(txId);
				}
				if(!tx.IsCoinBase)
					foreach(var input in tx.Inputs)
					{
						ids.Add(input.PrevOut.Hash);
					}
			}
			return ids.ToArray();
		}
	}
}
