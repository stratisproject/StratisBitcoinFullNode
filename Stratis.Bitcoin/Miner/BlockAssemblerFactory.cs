using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.MemoryPool;

namespace Stratis.Bitcoin.Miner
{
    public class BlockAssemblerFactory
    {
	    private readonly ConsensusLoop consensusLoop;
	    private readonly Network network;
	    private readonly ConcurrentChain chain;
	    private readonly MempoolScheduler mempoolScheduler;
	    private readonly TxMempool mempool;
	    private readonly IDateTimeProvider dateTimeProvider;

	    public BlockAssemblerFactory(ConsensusLoop consensusLoop, Network network, ConcurrentChain chain,
		    MempoolScheduler mempoolScheduler, TxMempool mempool,
		    IDateTimeProvider dateTimeProvider)
	    {
		    this.consensusLoop = consensusLoop;
		    this.network = network;
		    this.chain = chain;
		    this.mempoolScheduler = mempoolScheduler;
		    this.mempool = mempool;
		    this.dateTimeProvider = dateTimeProvider;
	    }

	    public BlockAssembler Create(BlockAssembler.Options options = null)
	    {
		    return new BlockAssembler(this.consensusLoop, this.network, this.chain, this.mempoolScheduler, this.mempool,
			    this.dateTimeProvider, options);
	    }
    }
}
