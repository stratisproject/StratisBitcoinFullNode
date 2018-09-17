using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    public class ConsensusRulesPerformanceCounter
    {
        public IDisposable MeasureRuleExecutionTime(ConsensusRuleBase rule)
        {
            var stopwatch = new StopwatchDisposable(elapsed =>
            {
                // TODO save
            });

            return stopwatch;
        }
    }
}
