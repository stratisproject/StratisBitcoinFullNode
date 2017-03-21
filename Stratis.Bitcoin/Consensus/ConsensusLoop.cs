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
		public ConsensusLoop(ConsensusValidator validator, ConcurrentChain chain, CoinView utxoSet, LookaheadBlockPuller puller)
		{
			Guard.NotNull(validator, nameof(validator));
			Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(utxoSet, nameof(utxoSet));
			Guard.NotNull(puller, nameof(puller));
			
			_Validator = validator;
			_Chain = chain;
			_utxoSet = utxoSet;
			_Puller = puller;
			Initialize();
		}

		private readonly LookaheadBlockPuller _Puller;
		public LookaheadBlockPuller Puller
		{
			get
			{
				return _Puller;
			}
		}


		private readonly ConcurrentChain _Chain;
		public ConcurrentChain Chain
		{
			get
			{
				return _Chain;
			}
		}


		private readonly CoinView _utxoSet;
		public CoinView UTXOSet
		{
			get
			{
				return _utxoSet;
			}
		}


		private readonly ConsensusValidator _Validator;
		public ConsensusValidator Validator
		{
			get
			{
				return _Validator;
			}
		}

		StopWatch watch = new StopWatch();

		private ChainedBlock _Tip;
		private ThresholdConditionCache bip9;

		public ChainedBlock Tip
		{
			get
			{
				return _Tip;
			}
		}

		public ThresholdConditionCache BIP9
		{
			get
			{
				return bip9;
			}
		}

		private void Initialize()
		{
			var utxoHash = _utxoSet.GetBlockHashAsync().GetAwaiter().GetResult();
			while(true)
			{
				_Tip = Chain.GetBlock(utxoHash);
				if(_Tip != null)
					break;
				utxoHash = _utxoSet.Rewind().GetAwaiter().GetResult();
			}
			Puller.SetLocation(Tip);
			bip9 = new ThresholdConditionCache(_Validator.ConsensusParams);
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
			lock(bip9)
			{
				var states = bip9.GetStates(block.Previous);
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
								_Tip = rewinded;
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
			ContextInformation context;
			ConsensusFlags flags;
			using (watch.Start(o => Validator.PerformanceCounter.AddBlockProcessingTime(o)))
			{
				Validator.CheckBlockHeader(result.Block.Header);
				if (result.Block.Header.HashPrevBlock != Tip.HashBlock)
					return; // reorg
				result.ChainedBlock = new ChainedBlock(result.Block.Header, result.Block.Header.GetHash(), Tip);
				result.ChainedBlock = Chain.GetBlock(result.ChainedBlock.HashBlock) ?? result.ChainedBlock;
					//Liberate from memory the block created above if possible
				context = new ContextInformation(result.ChainedBlock, Validator.ConsensusParams);
				Validator.ContextualCheckBlockHeader(result.Block.Header, context);
				flags = GetFlags(result.ChainedBlock);
				Validator.ContextualCheckBlock(result.Block, flags, context);
				Validator.CheckBlock(result.Block);
			}

			var set = new UnspentOutputSet();
			using (watch.Start(o => Validator.PerformanceCounter.AddUTXOFetchingTime(o)))
			{
				var ids = GetIdsToFetch(result.Block, flags.EnforceBIP30);
				var coins = UTXOSet.FetchCoinsAsync(ids).GetAwaiter().GetResult();
				set.SetCoins(coins);
			}

			TryPrefetchAsync(flags);
			using (watch.Start(o => Validator.PerformanceCounter.AddBlockProcessingTime(o)))
			{
				Validator.ExecuteBlock(result.Block, result.ChainedBlock, flags, set, null);
			}

			UTXOSet.SaveChangesAsync(set.GetCoins(UTXOSet), null, Tip.HashBlock, result.ChainedBlock.HashBlock);

			_Tip = result.ChainedBlock;
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
