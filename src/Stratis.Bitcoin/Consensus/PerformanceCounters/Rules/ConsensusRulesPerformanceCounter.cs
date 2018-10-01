using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus.PerformanceCounters.Rules
{
    /// <summary>Measures rules average execution time.</summary>
    [NoTrace]
    public class ConsensusRulesPerformanceCounter
    {
        /// <summary>List of rules registered for performance tracking.</summary>
        private readonly IList<RuleItem> registeredRules;

        /// <summary>Snapshot that is currently being populated.</summary>
        private ConsensusRulesPerformanceSnapshot currentSnapshot;

        public ConsensusRulesPerformanceCounter(IConsensus consensus)
        {
            this.registeredRules = new List<RuleItem>();

            this.RegisterRulesCollection(consensus.HeaderValidationRules.Select(x => x as IConsensusRuleBase), RuleType.Header);
            this.RegisterRulesCollection(consensus.IntegrityValidationRules.Select(x => x as IConsensusRuleBase), RuleType.Integrity);
            this.RegisterRulesCollection(consensus.PartialValidationRules.Select(x => x as IConsensusRuleBase), RuleType.Partial);
            this.RegisterRulesCollection(consensus.FullValidationRules.Select(x => x as IConsensusRuleBase), RuleType.Full);

            this.currentSnapshot = new ConsensusRulesPerformanceSnapshot(this.registeredRules);
        }

        private void RegisterRulesCollection(IEnumerable<IConsensusRuleBase> rules, RuleType rulesType)
        {
            foreach (IConsensusRuleBase rule in rules)
            {
                this.registeredRules.Add(new RuleItem()
                {
                    RuleName = rule.GetType().Name,
                    RuleType = rulesType,
                    RuleReferenceInstance = rule
                });
            }
        }

        /// <summary>Measures the rule execution time and adds this sample to performance counter.</summary>
        /// <param name="rule">Rule being measured.</param>
        /// <returns><see cref="IDisposable"/> that should be disposed after rule has finished it's execution.</returns>
        public IDisposable MeasureRuleExecutionTime(IConsensusRuleBase rule)
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                RulePerformance performance = this.currentSnapshot.PerformanceInfo[rule];

                Interlocked.Increment(ref performance.CalledTimes);
                Interlocked.Add(ref performance.ExecutionTimesTicks, elapsedTicks);
            });

            return stopwatch;
        }

        /// <summary>Takes current snapshot.</summary>
        /// <remarks>Not thread-safe. Caller should ensure that it's not called from different threads at once.</remarks>
        public ConsensusRulesPerformanceSnapshot TakeSnapshot()
        {
            var newSnapshot = new ConsensusRulesPerformanceSnapshot(this.registeredRules);
            ConsensusRulesPerformanceSnapshot previousSnapshot = this.currentSnapshot;
            this.currentSnapshot = newSnapshot;

            return previousSnapshot;
        }
    }
}
