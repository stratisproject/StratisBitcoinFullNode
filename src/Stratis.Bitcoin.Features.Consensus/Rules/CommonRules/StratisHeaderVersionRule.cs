﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// Checks if <see cref="StratisMain"/> network block's header has a valid block version.
    /// </summary>
    [HeaderValidationRule(CanSkipValidation = true)]
    public class StratisHeaderVersionRule : HeaderVersionRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated.</exception>
        public override Task RunAsync(RuleContext context)
        {
            Guard.NotNull(context.ConsensusTip, nameof(context.ConsensusTip));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeader;

            // TODO: Incorporate ComputeBlockVersion so that Stratis network gets proper BIP9 support with validation
            if (chainedHeader.Header.Version < 7)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }

            return Task.CompletedTask;
        }

        public override int ComputeBlockVersion(ChainedHeader prevChainedHeader, NBitcoin.Consensus consensus)
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