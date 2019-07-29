using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;

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
    [Obsolete("This class should be removed now that rules get registered in network")]
    public interface IRuleRegistration
    {
        [Obsolete]
        void RegisterRules(IServiceCollection service);
    }
}