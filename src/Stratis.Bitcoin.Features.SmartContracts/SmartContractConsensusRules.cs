using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Extension of consensus rules that provide access to a store based on UTXO (Unspent transaction outputs).
    /// </summary>
    public sealed class SmartContractConsensusRules : PowConsensusRules
    {
        public SmartContractConsensusRules(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ConcurrentChain chain, NodeDeployments nodeDeployments, ConsensusSettings consensusSettings, ICheckpoints checkpoints, CoinView utxoSet, ILookaheadBlockPuller puller)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, puller)
        {
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip)
        {
            return new PowRuleContext(validationContext, this.Network.Consensus, consensusTip);
        }
    }
}