using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// <summary>
        /// Computes what the block version of a newly created block should be, given a previous header and the
        /// current set of BIP9 deployments defined in the consensus.
        /// </summary>
        /// <param name="prevChainedHeader">The header of the previous block in the chain.</param>
        /// <param name="consensus">The current network's consensus parameters.</param>
        /// <remarks>This method is currently used during block creation only. Different nodes may not implement
        /// BIP9, or may disagree about what the current valid set of deployments are. It is therefore not strictly
        /// possible to validate a block version number in anything more than general terms.</remarks>
        public int ComputeBlockVersion(ChainedHeader prevChainedHeader, NBitcoin.Consensus consensus)
        {
            uint version = ThresholdConditionCache.VersionbitsTopBits;
            var thresholdConditionCache = new ThresholdConditionCache(consensus);

            IEnumerable<BIP9Deployments> deployments = Enum.GetValues(typeof(BIP9Deployments)).OfType<BIP9Deployments>();

            foreach (BIP9Deployments deployment in deployments)
            {
                ThresholdState state = thresholdConditionCache.GetState(prevChainedHeader, deployment);
                if ((state == ThresholdState.LockedIn) || (state == ThresholdState.Started))
                    version |= thresholdConditionCache.Mask(deployment);
            }

            return (int)version;
        }
    }
}