using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    public class PoAHeaderSignatureRule : HeaderValidationConsensusRule
    {
        public override void Run(RuleContext context)
        {
            // TODO POA implement rule
            // first check timestamp and estimate which pubkey should be used. Then check signature against that pubkey.
            // (like PosBlockSignatureRule but for header's sig),
        }
    }
}
