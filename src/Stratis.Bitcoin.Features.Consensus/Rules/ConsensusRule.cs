using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    public abstract class ConsensusRule
    {
        public ILogger Logger { get; set; }

        public ConsensusRules Parent { get; set; }

        public virtual IEnumerable<Type> Dependencies()
        {
            return Enumerable.Empty<Type>();
        }

        public virtual bool CanSkipValidation => false;

        public abstract Task RunAsync(ContextInformation context);
    }

    public abstract class SkipValidationConsensusRule : ConsensusRule
    {
        public override bool CanSkipValidation => true;
    }
}