﻿using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;
using System;

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

    public class ContextInformation
    {
        public NBitcoin.Consensus Consensus { get; set; }

        public DateTimeOffset Time { get; set; }

        public ContextBlockInformation BestBlock { get; set; }

        public ContextStakeInformation Stake { get; set; }

        public Target NextWorkRequired { get; set; }

        public BlockItem BlockItem { get; set; }

        public DeploymentFlags Flags { get; set; }

        public UnspentOutputSet Set { get; set; }

        public bool CheckMerkleRoot { get; set; }

        public bool CheckPow { get; set; }

        public bool OnlyCheck { get; set; }

        public bool IsPoS
        {
            get { return this.Stake != null; }
        }

        public ContextInformation()
        {
        }

        public ContextInformation(BlockItem blockItem, NBitcoin.Consensus consensus)
        {
            Guard.NotNull(blockItem, nameof(blockItem));
            Guard.NotNull(consensus, nameof(consensus));

            this.BlockItem = blockItem;
            this.Consensus = consensus;

            // TODO: adding flags to determine the flow of logic is not ideal
            // a refator is in depbate on moving to a consensus rules engine
            // this will remove hte need for flags as a validation will
            // only use the required rules (i.e if the check pow rule will be ommited form the flow)
            this.CheckPow = true;
            this.CheckMerkleRoot = true;
            this.OnlyCheck = false;
        }

        public void SetBestBlock()
        {
            this.BestBlock = new ContextBlockInformation(this.BlockItem.ChainedBlock.Previous, this.Consensus);
            this.Time = DateTimeOffset.UtcNow;
        }

        public void SetStake()
        {
            this.Stake = new ContextStakeInformation
            {
                BlockStake = new BlockStake(this.BlockItem.Block)
            };
        }
    }
}
