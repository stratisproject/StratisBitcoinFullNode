using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.Miner
{
	public abstract class AssemblerFactory
	{
		public abstract BlockAssembler Create(AssemblerOptions options = null);
	}

	public class PowAssemblerFactory : AssemblerFactory
	{
	    protected readonly ConsensusLoop consensusLoop;
		protected readonly Network network;
		protected readonly ConcurrentChain chain;
		protected readonly MempoolScheduler mempoolScheduler;
		protected readonly TxMempool mempool;
		protected readonly IDateTimeProvider dateTimeProvider;
		protected readonly StakeChain stakeChain;

	    public PowAssemblerFactory(ConsensusLoop consensusLoop, Network network, ConcurrentChain chain,
		    MempoolScheduler mempoolScheduler, TxMempool mempool,
		    IDateTimeProvider dateTimeProvider, StakeChain stakeChain = null)
	    {
		    this.consensusLoop = consensusLoop;
		    this.network = network;
		    this.chain = chain;
		    this.mempoolScheduler = mempoolScheduler;
		    this.mempool = mempool;
		    this.dateTimeProvider = dateTimeProvider;
		    this.stakeChain = stakeChain;
	    }

	    public override BlockAssembler Create(AssemblerOptions options = null)
	    {
		    return new PowBlockAssembler(this.consensusLoop, this.network, this.chain, this.mempoolScheduler, this.mempool, this.dateTimeProvider, options);
	    }
	}

	public class PosAssemblerFactory : AssemblerFactory
	{
		protected readonly ConsensusLoop consensusLoop;
		protected readonly Network network;
		protected readonly ConcurrentChain chain;
		protected readonly MempoolScheduler mempoolScheduler;
		protected readonly TxMempool mempool;
		protected readonly IDateTimeProvider dateTimeProvider;
		protected readonly StakeChain stakeChain;

		public PosAssemblerFactory(ConsensusLoop consensusLoop, Network network, ConcurrentChain chain,
			MempoolScheduler mempoolScheduler, TxMempool mempool,
			IDateTimeProvider dateTimeProvider, StakeChain stakeChain = null)
		{
			this.consensusLoop = consensusLoop;
			this.network = network;
			this.chain = chain;
			this.mempoolScheduler = mempoolScheduler;
			this.mempool = mempool;
			this.dateTimeProvider = dateTimeProvider;
			this.stakeChain = stakeChain;
		}

		public override BlockAssembler Create(AssemblerOptions options = null)
		{
			return new PosBlockAssembler(this.consensusLoop, this.network, this.chain, this.mempoolScheduler, this.mempool,
				this.dateTimeProvider, stakeChain, options);
		}

	}
}
