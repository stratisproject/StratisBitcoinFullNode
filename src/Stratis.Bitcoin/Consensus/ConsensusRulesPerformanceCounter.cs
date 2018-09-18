using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>Measures rules average execution time.</summary>
    public class ConsensusRulesPerformanceCounter
    {
        private readonly Dictionary<string, RuleItem> rulesInfo;

        /// <summary>Protects access to <see cref="rulesInfo"/>.</summary>
        private readonly object locker;

        private const int MaxSamples = 1000;

        public ConsensusRulesPerformanceCounter()
        {
            this.rulesInfo = new Dictionary<string, RuleItem>();
            this.locker = new object();
        }

        /// <summary>Measures the rule execution time and adds this sample to performance counter.</summary>
        /// <param name="rule">Rule being measured.</param>
        /// <param name="ruleType">Type of the rule.</param>
        /// <returns><see cref="IDisposable"/> that should be disposed after rule has finished it's execution.</returns>
        public IDisposable MeasureRuleExecutionTime(ConsensusRuleBase rule, RuleType ruleType)
        {
            var stopwatch = new StopwatchDisposable(elapsedTicks =>
            {
                string ruleName = rule.GetType().Name;

                lock (this.locker)
                {
                    if (this.rulesInfo.ContainsKey(ruleName))
                    {
                        this.rulesInfo[ruleName].ExecutionTime.AddSample(elapsedTicks);
                    }
                    else
                    {
                        var ruleItem = new RuleItem()
                        {
                            RuleName = ruleName,
                            RuleType = ruleType,
                            ExecutionTime = new AverageCalculator(MaxSamples)
                        };

                        ruleItem.ExecutionTime.AddSample(elapsedTicks);
                        this.rulesInfo.Add(ruleName, ruleItem);
                    }
                }
            });

            return stopwatch;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine();
            builder.AppendLine("======ConsensusRules Bench======");

            int ticksPerMs = 10_000;

            lock (this.locker)
            {
                if (this.rulesInfo.Count == 0)
                {
                    builder.AppendLine("No samples...");
                    return builder.ToString();
                }

                builder.AppendLine($"Using up to {MaxSamples} most recent samples.");

                foreach (IGrouping<RuleType, RuleItem> rulesGroup in this.rulesInfo.Values.GroupBy(x => x.RuleType))
                {
                    double totalRunningTimeMs = Math.Round(rulesGroup.Sum(x => x.ExecutionTime.Average) / ticksPerMs, 4);

                    builder.AppendLine($"{rulesGroup.Key} validation rules. Total execution time: {totalRunningTimeMs} ms.");

                    foreach (RuleItem rule in rulesGroup)
                    {
                        double milliseconds = Math.Round(rule.ExecutionTime.Average / ticksPerMs, 4);
                        double percentage = Math.Round((milliseconds / totalRunningTimeMs) * 100.0);

                        builder.AppendLine($"    {rule.RuleName.PadRight(50, '-')}{(milliseconds + " ms").PadRight(12, '-')}{percentage} %");
                    }

                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private class RuleItem
        {
            public string RuleName;
            public RuleType RuleType;
            public AverageCalculator ExecutionTime;
        }
    }
}
