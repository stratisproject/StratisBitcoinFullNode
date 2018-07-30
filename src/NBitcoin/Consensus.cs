using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Rules;

namespace NBitcoin
{
    public class Consensus : IConsensus
    {
        /// <inheritdoc />
        public long CoinbaseMaturity { get; set; }

        /// <inheritdoc />
        public Money PremineReward { get; }

        /// <inheritdoc />
        public long PremineHeight { get; }

        /// <inheritdoc />
        public Money ProofOfWorkReward { get; }

        /// <inheritdoc />
        public Money ProofOfStakeReward { get; }

        /// <inheritdoc />
        public uint MaxReorgLength { get; }

        /// <inheritdoc />
        public long MaxMoney { get; }

        public ConsensusOptions Options { get; set; }

        public Consensus(IConsensus consensus)
        {
            this.BuriedDeployments = new BuriedDeploymentsArray();
            this.BIP9Deployments = new BIP9DeploymentsArray();
            this.Rules = new List<IConsensusRule>();
            this.CoinbaseMaturity = consensus.CoinbaseMaturity;
            this.PremineReward = consensus.PremineReward;
            this.PremineHeight = consensus.PremineHeight;
            this.ProofOfWorkReward = consensus.ProofOfWorkReward;
            this.ProofOfStakeReward = consensus.ProofOfStakeReward;
            this.MaxReorgLength = consensus.MaxReorgLength;
            this.MaxMoney = consensus.MaxMoney;
            this.Options = consensus.Options;
            this.BuriedDeployments[NBitcoin.BuriedDeployments.BIP34] = consensus.BuriedDeployments[NBitcoin.BuriedDeployments.BIP34];
            this.BuriedDeployments[NBitcoin.BuriedDeployments.BIP65] = consensus.BuriedDeployments[NBitcoin.BuriedDeployments.BIP65];
            this.BuriedDeployments[NBitcoin.BuriedDeployments.BIP66] = consensus.BuriedDeployments[NBitcoin.BuriedDeployments.BIP66];
            this.BIP9Deployments[NBitcoin.BIP9Deployments.CSV] = consensus.BIP9Deployments[NBitcoin.BIP9Deployments.CSV];
            this.BIP9Deployments[NBitcoin.BIP9Deployments.Segwit] = consensus.BIP9Deployments[NBitcoin.BIP9Deployments.Segwit];
            this.BIP9Deployments[NBitcoin.BIP9Deployments.TestDummy] = consensus.BIP9Deployments[NBitcoin.BIP9Deployments.TestDummy];
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

        public BuriedDeploymentsArray BuriedDeployments { get; }

        public BIP9DeploymentsArray BIP9Deployments { get; }

        public int SubsidyHalvingInterval { get; }

        public int MajorityEnforceBlockUpgrade { get; }

        public int MajorityRejectBlockOutdated { get; }

        public int MajorityWindow { get; }

        public uint256 BIP34Hash { get; }

        public Target PowLimit { get; }

        public TimeSpan PowTargetTimespan { get; }

        public TimeSpan PowTargetSpacing { get; }

        public bool PowAllowMinDifficultyBlocks { get; }

        public bool PowNoRetargeting { get; }

        public uint256 HashGenesisBlock { get; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; }

        public long DifficultyAdjustmentInterval
        {
            get { return ((long)this.PowTargetTimespan.TotalSeconds / (long)this.PowTargetSpacing.TotalSeconds); }
        }

        public int MinerConfirmationWindow { get; set; }

        public int RuleChangeActivationThreshold { get; set; }

        /// <inheritdoc />
        public int CoinType { get; }

        public BigInteger ProofOfStakeLimit { get; }

        public BigInteger ProofOfStakeLimitV2 { get; }

        /// <inheritdoc />        
        public int LastPOWBlock { get; set; }

        /// <inheritdoc />
        public bool IsProofOfStake { get; }

        /// <inheritdoc />
        public uint256 DefaultAssumeValid { get; }

        /// <inheritdoc />
        public ConsensusFactory ConsensusFactory { get; }

        /// <summary>
        /// Rules specific to the given network.
        /// </summary>
        public ICollection<IConsensusRule> Rules { get; set; }
    }
}