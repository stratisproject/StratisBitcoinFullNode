using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    /// <summary>
    /// Used to check that people don't try and send funds to contracts via P2PKH.
    /// </summary>
    public class P2PKHNotContractMempoolRule : MempoolRule
    {
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public P2PKHNotContractMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IStateRepositoryRoot stateRepositoryRoot,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            P2PKHNotContractRule.CheckTransaction(this.stateRepositoryRoot, context.Transaction);
        }
    }
}