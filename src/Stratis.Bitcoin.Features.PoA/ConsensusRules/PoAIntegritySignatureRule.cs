using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    public class PoAIntegritySignatureRule : IntegrityValidationConsensusRule
    {
        /// <inheritdoc />
        public override void Run(RuleContext context)
        {
            // TODO POA implement rule
            // Checks that signature from header we wanted to download block data for is equal to signature in block we've received.
        }
    }
}
