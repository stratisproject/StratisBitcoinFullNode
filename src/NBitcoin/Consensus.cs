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
        public uint MaxReorgLength { get; private set; }

        /// <inheritdoc />
        public long MaxMoney { get; }

        public ConsensusOptions Options { get; set; }

        public BuriedDeploymentsArray BuriedDeployments { get; }

        public IBIP9DeploymentsArray BIP9Deployments { get; }

        public int SubsidyHalvingInterval { get; }

        public int MajorityEnforceBlockUpgrade { get; }

        public int MajorityRejectBlockOutdated { get; }

        public int MajorityWindow { get; }

        public uint256 BIP34Hash { get; }

        public Target PowLimit { get; }

        public TimeSpan PowTargetTimespan { get; }

        public TimeSpan PowTargetSpacing { get; }

        public bool PowAllowMinDifficultyBlocks { get; }

        /// <inheritdoc />
        public bool PosNoRetargeting { get; }

        /// <inheritdoc />
        public bool PowNoRetargeting { get; }

        public uint256 HashGenesisBlock { get; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; }

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

        /// <inheritdoc />
        public List<IIntegrityValidationConsensusRule> IntegrityValidationRules { get; set; }

        /// <inheritdoc />
        public List<IHeaderValidationConsensusRule> HeaderValidationRules { get; set; }

        /// <inheritdoc />
        public List<IPartialValidationConsensusRule> PartialValidationRules { get; set; }

        /// <inheritdoc />
        public List<IFullValidationConsensusRule> FullValidationRules { get; set; }

        public Consensus(
            ConsensusFactory consensusFactory,
            ConsensusOptions consensusOptions,
            int coinType,
            uint256 hashGenesisBlock,
            int subsidyHalvingInterval,
            int majorityEnforceBlockUpgrade,
            int majorityRejectBlockOutdated,
            int majorityWindow,
            BuriedDeploymentsArray buriedDeployments,
            IBIP9DeploymentsArray bip9Deployments,
            uint256 bip34Hash,
            int ruleChangeActivationThreshold,
            int minerConfirmationWindow,
            uint maxReorgLength,
            uint256 defaultAssumeValid,
            long maxMoney,
            long coinbaseMaturity,
            long premineHeight,
            Money premineReward,
            Money proofOfWorkReward,
            TimeSpan powTargetTimespan,
            TimeSpan powTargetSpacing,
            bool powAllowMinDifficultyBlocks,
            bool posNoRetargeting,
            bool powNoRetargeting,
            Target powLimit,
            uint256 minimumChainWork,
            bool isProofOfStake,
            int lastPowBlock,
            BigInteger proofOfStakeLimit,
            BigInteger proofOfStakeLimitV2,
            Money proofOfStakeReward)
        {
            this.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>();
            this.HeaderValidationRules = new List<IHeaderValidationConsensusRule>();
            this.PartialValidationRules = new List<IPartialValidationConsensusRule>();
            this.FullValidationRules = new List<IFullValidationConsensusRule>();
            this.CoinbaseMaturity = coinbaseMaturity;
            this.PremineReward = premineReward;
            this.PremineHeight = premineHeight;
            this.ProofOfWorkReward = proofOfWorkReward;
            this.ProofOfStakeReward = proofOfStakeReward;
            this.MaxReorgLength = maxReorgLength;
            this.MaxMoney = maxMoney;
            this.Options = consensusOptions;
            this.BuriedDeployments = buriedDeployments;
            this.BIP9Deployments = bip9Deployments;
            this.SubsidyHalvingInterval = subsidyHalvingInterval;
            this.MajorityEnforceBlockUpgrade = majorityEnforceBlockUpgrade;
            this.MajorityRejectBlockOutdated = majorityRejectBlockOutdated;
            this.MajorityWindow = majorityWindow;
            this.BIP34Hash = bip34Hash;
            this.PowLimit = powLimit;
            this.PowTargetTimespan = powTargetTimespan;
            this.PowTargetSpacing = powTargetSpacing;
            this.PowAllowMinDifficultyBlocks = powAllowMinDifficultyBlocks;
            this.PosNoRetargeting = posNoRetargeting;
            this.PowNoRetargeting = powNoRetargeting;
            this.HashGenesisBlock = hashGenesisBlock;
            this.MinimumChainWork = minimumChainWork;
            this.MinerConfirmationWindow = minerConfirmationWindow;
            this.RuleChangeActivationThreshold = ruleChangeActivationThreshold;
            this.CoinType = coinType;
            this.ProofOfStakeLimit = proofOfStakeLimit;
            this.ProofOfStakeLimitV2 = proofOfStakeLimitV2;
            this.LastPOWBlock = lastPowBlock;
            this.IsProofOfStake = isProofOfStake;
            this.DefaultAssumeValid = defaultAssumeValid;
            this.ConsensusFactory = consensusFactory;
        }
    }
}