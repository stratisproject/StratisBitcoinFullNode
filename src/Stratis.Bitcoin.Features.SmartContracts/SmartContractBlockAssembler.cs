using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractBlockAssembler : PowBlockAssembler
    {
        public SmartContractBlockAssembler(IConsensusLoop consensusLoop, Network network, MempoolSchedulerLock mempoolLock, ITxMempool mempool, IDateTimeProvider dateTimeProvider, ChainedBlock chainTip, ILoggerFactory loggerFactory, AssemblerOptions options = null) : base(consensusLoop, network, mempoolLock, mempool, dateTimeProvider, chainTip, loggerFactory, options){}

        protected override void AddToBlock(TxMempoolEntry iter)
        {
            base.AddToBlock(iter);
        }
    }
}
