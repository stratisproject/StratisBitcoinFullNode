using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Rules;

namespace Stratis.Bitcoin.Tests.Common
{
    /// <summary>
    /// A mutable implementation of <see cref="IConsensus"/>. For testing only.
    /// </summary>
    public class MutableTestConsensus : IConsensus
    {
        public MutableTestConsensus(IConsensus consensus)
        {
            this.Rules = consensus.Rules;
            this.CoinbaseMaturity = consensus.CoinbaseMaturity;
            this.PremineReward = consensus.PremineReward;
            this.PremineHeight = consensus.PremineHeight;
            this.ProofOfWorkReward = consensus.ProofOfWorkReward;
            this.ProofOfStakeReward = consensus.ProofOfStakeReward;
            this.MaxReorgLength = consensus.MaxReorgLength;
            this.MaxMoney = consensus.MaxMoney;
            this.Options = consensus.Options;
            this.BuriedDeployments = consensus.BuriedDeployments;
            this.BIP9Deployments = consensus.BIP9Deployments;
            this.SubsidyHalvingInterval = consensus.SubsidyHalvingInterval;
            this.MajorityEnforceBlockUpgrade = consensus.MajorityEnforceBlockUpgrade;
            this.MajorityRejectBlockOutdated = consensus.MajorityRejectBlockOutdated;
            this.MajorityWindow = consensus.MajorityWindow;
            this.BIP34Hash = consensus.BIP34Hash;
            this.PowLimit = consensus.PowLimit;
            this.PowTargetTimespan = consensus.PowTargetTimespan;
            this.PowTargetSpacing = consensus.PowTargetSpacing;
            this.PowAllowMinDifficultyBlocks = consensus.PowAllowMinDifficultyBlocks;
            this.PowNoRetargeting = consensus.PowNoRetargeting;
            this.HashGenesisBlock = consensus.HashGenesisBlock;
            this.MinimumChainWork = consensus.MinimumChainWork;
            this.MinerConfirmationWindow = consensus.MinerConfirmationWindow;
            this.RuleChangeActivationThreshold = consensus.RuleChangeActivationThreshold;
            this.CoinType = consensus.CoinType;
            this.ProofOfStakeLimit = consensus.ProofOfStakeLimit;
            this.ProofOfStakeLimitV2 = consensus.ProofOfStakeLimitV2;
            this.LastPOWBlock = consensus.LastPOWBlock;
            this.IsProofOfStake = consensus.IsProofOfStake;
            this.DefaultAssumeValid = consensus.DefaultAssumeValid;
            this.ConsensusFactory = consensus.ConsensusFactory;
        }

        public long CoinbaseMaturity { get; set; }
        public Money PremineReward { get; set; }
        public long PremineHeight { get; set; }
        public Money ProofOfWorkReward { get; set; }
        public Money ProofOfStakeReward { get; set; }
        public uint MaxReorgLength { get; set; }
        public long MaxMoney { get; set; }
        public ConsensusOptions Options { get; set; }
        public BuriedDeploymentsArray BuriedDeployments { get; set; }
        public BIP9DeploymentsArray BIP9Deployments { get; set; }
        public int SubsidyHalvingInterval { get; set; }
        public int MajorityEnforceBlockUpgrade { get; set; }
        public int MajorityRejectBlockOutdated { get; set; }
        public int MajorityWindow { get; set; }
        public uint256 BIP34Hash { get; set; }
        public Target PowLimit { get; set; }
        public TimeSpan PowTargetTimespan { get; set; }
        public TimeSpan PowTargetSpacing { get; set; }
        public bool PowAllowMinDifficultyBlocks { get; set; }
        public bool PowNoRetargeting { get; set; }
        public uint256 HashGenesisBlock { get; set; }
        public uint256 MinimumChainWork { get; set; }
        public int MinerConfirmationWindow { get; set; }
        public int RuleChangeActivationThreshold { get; set; }
        public int CoinType { get; set; }
        public BigInteger ProofOfStakeLimit { get; set; }
        public BigInteger ProofOfStakeLimitV2 { get; set; }
        public int LastPOWBlock { get; set; }
        public bool IsProofOfStake { get; set; }
        public uint256 DefaultAssumeValid { get; set; }
        public ConsensusFactory ConsensusFactory { get; set; }
        public ICollection<IConsensusRule> Rules { get; set; }
    }
}