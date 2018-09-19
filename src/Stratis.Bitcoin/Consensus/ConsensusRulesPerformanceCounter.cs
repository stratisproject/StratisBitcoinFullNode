using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>Measures rules average execution time.</summary>
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

    public class ConsensusRulesPerformanceSnapshot
    {
        internal Dictionary<IConsensusRuleBase, RulePerformance> PerformanceInfo { get; }

        internal ConsensusRulesPerformanceSnapshot(IEnumerable<RuleItem> rulesToTrack)
        {
            this.PerformanceInfo = new Dictionary<IConsensusRuleBase, RulePerformance>();

            foreach (RuleItem rule in rulesToTrack)
            {
                var perf = new RulePerformance(rule);
                this.PerformanceInfo.Add(rule.RuleReferenceInstance, perf);
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine();
            builder.AppendLine("======ConsensusRules Bench======");

            if (this.PerformanceInfo.All(x => x.Value.CalledTimes == 0))
            {
                builder.AppendLine("No samples...");
                return builder.ToString();
            }

            foreach (IGrouping<RuleType, RulePerformance> rulesGroup in this.PerformanceInfo.Values.GroupBy(x => x.RuleType))
            {
                int ruleGroupTotalCalls = rulesGroup.Max(x => x.CalledTimes);

                if (ruleGroupTotalCalls == 0)
                    continue;

                long totalExecutionTimeTicks = rulesGroup.Sum(x => x.ExecutionTimesTicks);
                double avgGroupExecutionTimeMs = Math.Round(TimeSpan.FromTicks(totalExecutionTimeTicks / ruleGroupTotalCalls).TotalMilliseconds, 4);

                builder.AppendLine($"{rulesGroup.Key} validation rules. Average total execution time: {avgGroupExecutionTimeMs} ms.");

                foreach (RulePerformance rule in rulesGroup)
                {
                    if (rule.CalledTimes == 0)
                    {
                        builder.AppendLine($"    {rule.RuleName.PadRight(50, '-')}{("No Samples").PadRight(12, '-')}");
                        continue;
                    }

                    double avgExecutionTimeMs = Math.Round((TimeSpan.FromTicks(rule.ExecutionTimesTicks / rule.CalledTimes).TotalMilliseconds), 4);

                    // % from average execution time for the group.
                    double percentage = Math.Round((avgExecutionTimeMs / avgGroupExecutionTimeMs) * 100.0);

                    builder.AppendLine($"    {rule.RuleName.PadRight(50, '-')}{(avgExecutionTimeMs + " ms").PadRight(12, '-')}{percentage} %");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    internal class RuleItem
    {
        public string RuleName { get; set; }

        public RuleType RuleType { get; set; }

        public IConsensusRuleBase RuleReferenceInstance { get; set; }
    }

    internal class RulePerformance : RuleItem
    {
        public int CalledTimes;

        public long ExecutionTimesTicks;

        public RulePerformance(RuleItem rule)
        {
            this.RuleName = rule.RuleName;
            this.RuleType = rule.RuleType;
            this.RuleReferenceInstance = rule.RuleReferenceInstance;

            this.CalledTimes = 0;
            this.ExecutionTimesTicks = 0;
        }
    }

    public enum RuleType
    {
        Header,
        Integrity,
        Partial,
        Full
    }
}
