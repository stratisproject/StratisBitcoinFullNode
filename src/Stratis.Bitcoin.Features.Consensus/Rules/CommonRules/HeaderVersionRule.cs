using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public abstract class HeaderVersionRule : ConsensusRule
    {
        public abstract int ComputeBlockVersion(ChainedHeader prevChainedHeader, NBitcoin.Consensus consensus);
    }
}