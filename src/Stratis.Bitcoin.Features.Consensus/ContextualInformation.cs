﻿using System;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ContextBlockInformation
    {
        public BlockHeader Header { get; set; }

        public int Height { get; set; }

        public DateTimeOffset MedianTimePast { get; set; }

        public ContextBlockInformation()
        {
        }

        public ContextBlockInformation(ChainedBlock bestBlock, NBitcoin.Consensus consensus)
        {
            Guard.NotNull(bestBlock, nameof(bestBlock));

            this.Header = bestBlock.Header;
            this.Height = bestBlock.Height;
            this.MedianTimePast = bestBlock.GetMedianTimePast();
        }
    }

    public class ContextStakeInformation
    {
        public BlockStake BlockStake { get; set; }

        public Money TotalCoinStakeValueIn { get; set; }

        public uint256 HashProofOfStake { get; set; }

        public uint256 TargetProofOfStake { get; set; }
    }

    /// <summary>
    /// Context that contains variety of information regarding blocks validation and execution.
    /// </summary>
    public class RuleContext
    {
        public NBitcoin.Consensus Consensus { get; set; }

        public DateTimeOffset Time { get; set; }

        public ContextBlockInformation BestBlock { get; set; }

        public ContextStakeInformation Stake { get; set; }

        public Target NextWorkRequired { get; set; }

        public BlockValidationContext BlockValidationContext { get; set; }

        public DeploymentFlags Flags { get; set; }

        public UnspentOutputSet Set { get; set; }

        public bool CheckMerkleRoot { get; set; }

        public bool CheckPow { get; set; }

        /// <summary>Whether to skip block validation for this block due to either a checkpoint or assumevalid hash set.</summary>
        public bool SkipValidation { get; set; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedBlock ConsensusTip { get; set; }

        public bool IsPoS
        {
            get { return this.Stake != null; }
        }

        public RuleContext()
        {
        }

        public RuleContext(BlockValidationContext blockValidationContext, NBitcoin.Consensus consensus, ChainedBlock consensusTip)
        {
            Guard.NotNull(blockValidationContext, nameof(blockValidationContext));
            Guard.NotNull(consensus, nameof(consensus));

            this.BlockValidationContext = blockValidationContext;
            this.Consensus = consensus;
            this.ConsensusTip = consensusTip;

            // TODO: adding flags to determine the flow of logic is not ideal
            // a refator is in depbate on moving to a consensus rules engine
            // this will remove hte need for flags as a validation will
            // only use the required rules (i.e if the check pow rule will be ommited form the flow)
            this.CheckPow = true;
            this.CheckMerkleRoot = true;
        }

        public void SetBestBlock(DateTimeOffset now)
        {
            this.BestBlock = new ContextBlockInformation(this.BlockValidationContext.ChainedBlock.Previous, this.Consensus);
            this.Time = now;
        }

        public void SetStake()
        {
            this.Stake = new ContextStakeInformation
            {
                BlockStake = new BlockStake(this.BlockValidationContext.Block)
            };
        }
    }
}
