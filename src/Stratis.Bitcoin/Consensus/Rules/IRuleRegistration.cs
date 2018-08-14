using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <summary>
    /// An interface that will allow the registration of bulk consensus rules in to the engine.
    /// </summary>
    /// <remarks>
    /// It is important to note that there is high importance to the order the rules are registered
    /// with the engine, this is important for rules with dependencies on other rules.
    /// Rules are executed in the same order they are registered with the engine.
    /// </remarks>
    public interface IRuleRegistration
    {
        void RegisterRules(IConsensus consensus);
    }
}