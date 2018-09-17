using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusRulesPerformanceCounter
    {
        private readonly Dictionary<string, RuleItem> rulesInfo;

        /// <summary>Protects access to <see cref="rulesInfo"/>.</summary>
        private readonly object locker;

        private const int MaxSamples = 100;

        public ConsensusRulesPerformanceCounter()
        {
            this.rulesInfo = new Dictionary<string, RuleItem>();
            this.locker = new object();
        }

        public IDisposable MeasureRuleExecutionTime(ConsensusRuleBase rule, RuleType ruleType)
        {
            var stopwatch = new StopwatchDisposable(elapsed =>
            {
                string ruleName = rule.GetType().Name;

                lock (this.locker)
                {
                    if (this.rulesInfo.ContainsKey(ruleName))
                    {
                        this.rulesInfo[ruleName].ExecutionTime.AddSample(elapsed);
                    }
                    else
                    {
                        var ruleItem = new RuleItem()
                        {
                            RuleType = ruleType,
                            ExecutionTime = new AverageCalculator(MaxSamples)
                        };

                        ruleItem.ExecutionTime.AddSample(elapsed);
                        this.rulesInfo.Add(ruleName, ruleItem);
                    }
                }
            });

            return stopwatch;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            // TODO

            return builder.ToString();
        }

        private class RuleItem
        {
            public RuleType RuleType;
            public AverageCalculator ExecutionTime;
        }
    }
}
