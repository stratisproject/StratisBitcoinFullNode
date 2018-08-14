using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Rules;

namespace Stratis.Bitcoin.Consensus.Rules
{
    public abstract class BaseConsensusRuleBase : IBaseConsensusRule
    {
        /// <summary>Instance logger.</summary>
        public ILogger Logger { get; set; }

        /// <summary>The engine this rule belongs to.</summary>
        public ConsensusRuleEngine Parent { get; set; }

        /// <summary>
        /// Allow a rule to initialize itself.
        /// The rule can verify that other rules are present using the <see cref="IConsensusRuleEngine.Rules"/>.
        /// The rule can internally initialize its state.
        /// </summary>
        public virtual void Initialize()
        {
        }
    }

    /// <summary>An abstract rule for implementing consensus rules.</summary>
    public abstract class SyncBaseConsensusRule : BaseConsensusRuleBase, ISyncBaseConsensusRule
    {
        /// <summary>
        /// Execute the logic in the current rule.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        public virtual void Run(RuleContext context)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>An abstract rule for implementing consensus rules.</summary>
    public abstract class AsyncBaseConsensusRule : BaseConsensusRuleBase, IAsyncBaseConsensusRule
    {
        /// <summary>
        /// Execute the logic in the current rule in an async approach.
        /// If the validation of the rule fails a <see cref="ConsensusErrorException"/> will be thrown.
        /// </summary>
        /// <param name="context">The context that has all info that needs to be validated.</param>
        /// <returns>The execution task.</returns>
        public virtual Task RunAsync(RuleContext context)
        {
            throw new NotImplementedException();
        }
    }
}