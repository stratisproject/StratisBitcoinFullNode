using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolRuleContext
    {
        public Network Network { get; }

        public ITxMempool Mempool { get; } 

        public MempoolSettings Settings { get; }

        public ChainIndexer ChainIndexer { get; }

        public IConsensusRuleEngine ConsensusRules { get; }

        public FeeRate MinRelayTxFee { get; }

        public ILogger Logger { get; }

        public MempoolRuleContext(Network network, ITxMempool memPool, MempoolSettings settings, ChainIndexer chainIndexer, IConsensusRuleEngine consensusRules, FeeRate minRelayTxFee, ILogger logger)
        {
            this.Network = network;
            this.Mempool = memPool;
            this.Settings = settings;
            this.ChainIndexer = chainIndexer;
            this.ConsensusRules = consensusRules;
            this.MinRelayTxFee = minRelayTxFee;
            this.Logger = logger;
        }
    }
}