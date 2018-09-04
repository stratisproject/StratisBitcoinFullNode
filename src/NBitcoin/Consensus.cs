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
        public Money PremineReward { get; private set; }

        /// <inheritdoc />
        public long PremineHeight { get; private set; }

        /// <inheritdoc />
        public Money ProofOfWorkReward { get; private set; }

        /// <inheritdoc />
        public Money ProofOfStakeReward { get; private set; }

        /// <inheritdoc />
        public uint MaxReorgLength { get; private set; }

        /// <inheritdoc />
        public long MaxMoney { get; private set; }

        public ConsensusOptions Options { get; set; }

        public BuriedDeploymentsArray BuriedDeployments { get; private set; }

        public BIP9DeploymentsArray BIP9Deployments { get; private set; }

        public int SubsidyHalvingInterval { get; private set; }

        public int MajorityEnforceBlockUpgrade { get; private set; }

        public int MajorityRejectBlockOutdated { get; private set; }

        public int MajorityWindow { get; private set; }

        public uint256 BIP34Hash { get; private set; }

        public Target PowLimit { get; private set; }

        public TimeSpan PowTargetTimespan { get; private set; }

        public TimeSpan PowTargetSpacing { get; private set; }

        public bool PowAllowMinDifficultyBlocks { get; private set; }

        public bool PowNoRetargeting { get; private set; }

        public uint256 HashGenesisBlock { get; private set; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; private set; }

        public int MinerConfirmationWindow { get; set; }

        public int RuleChangeActivationThreshold { get; set; }

        /// <inheritdoc />
        public int CoinType { get; private set; }

        public BigInteger ProofOfStakeLimit { get; private set; }

        public BigInteger ProofOfStakeLimitV2 { get; private set; }

        /// <inheritdoc />
        public int LastPOWBlock { get; set; }

        /// <inheritdoc />
        public bool IsProofOfStake { get; private set; }

        /// <inheritdoc />
        public uint256 DefaultAssumeValid { get; private set; }

        /// <inheritdoc />
        public ConsensusFactory ConsensusFactory { get; private set; }

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
            BIP9DeploymentsArray bip9Deployments,
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

        /// <inheritdoc />
        public void Merge(IConsensus consensus)
        {
            if (consensus.BIP34Hash != null && consensus.BIP34Hash != uint256.Zero)
                this.BIP34Hash = consensus.BIP34Hash;

            if (consensus.BIP9Deployments != null)
                this.BIP9Deployments = consensus.BIP9Deployments;

            if (consensus.BuriedDeployments != null)
                this.BuriedDeployments = consensus.BuriedDeployments;

            if (consensus.CoinbaseMaturity != 0)
                this.CoinbaseMaturity = consensus.CoinbaseMaturity;

            if (consensus.CoinType != 0)
                this.CoinType = consensus.CoinType;

            if (consensus.ConsensusFactory != null)
                this.ConsensusFactory = consensus.ConsensusFactory;

            if (consensus.DefaultAssumeValid != null && consensus.DefaultAssumeValid != uint256.Zero)
                this.DefaultAssumeValid = consensus.DefaultAssumeValid;

            if (consensus.FullValidationRules != null)
                this.FullValidationRules = consensus.FullValidationRules;

            if (consensus.HashGenesisBlock != null && consensus.HashGenesisBlock != uint256.Zero)
                this.HashGenesisBlock = consensus.HashGenesisBlock;

            if (consensus.HeaderValidationRules != null)
                this.HeaderValidationRules = consensus.HeaderValidationRules;

            if (consensus.IntegrityValidationRules != null)
                this.IntegrityValidationRules = consensus.IntegrityValidationRules;

            if (consensus.IsProofOfStake)
                this.IsProofOfStake = consensus.IsProofOfStake;

            if (consensus.LastPOWBlock != 0)
                this.LastPOWBlock = consensus.LastPOWBlock;

            if (consensus.MajorityEnforceBlockUpgrade != 0)
                this.MajorityEnforceBlockUpgrade = consensus.MajorityEnforceBlockUpgrade;

            if (consensus.MajorityRejectBlockOutdated != 0)
                this.MajorityRejectBlockOutdated = consensus.MajorityRejectBlockOutdated;

            if (consensus.MajorityWindow != 0)
                this.MajorityWindow = consensus.MajorityWindow;

            if (consensus.MaxMoney != 0)
                this.MaxMoney = consensus.MaxMoney;

            if (consensus.MaxReorgLength != 0)
                this.MaxReorgLength = consensus.MaxReorgLength;

            if (consensus.MinerConfirmationWindow != 0)
                this.MinerConfirmationWindow = consensus.MinerConfirmationWindow;

            if (consensus.MinimumChainWork != null && consensus.MinimumChainWork != uint256.Zero)
                this.MinimumChainWork = consensus.MinimumChainWork;

            if (consensus.Options != null)
                this.Options = consensus.Options;

            if (consensus.PartialValidationRules != null)
                this.PartialValidationRules = consensus.PartialValidationRules;

            if (consensus.PowAllowMinDifficultyBlocks)
                this.PowAllowMinDifficultyBlocks = consensus.PowAllowMinDifficultyBlocks;

            if (consensus.PowLimit != null)
                this.PowLimit = consensus.PowLimit;

            if (consensus.PowNoRetargeting)
                this.PowNoRetargeting = consensus.PowNoRetargeting;

            if (consensus.PowTargetSpacing != null && consensus.PowTargetSpacing != TimeSpan.Zero)
                this.PowTargetSpacing = consensus.PowTargetSpacing;

            if (consensus.PowTargetTimespan != null && consensus.PowTargetTimespan != TimeSpan.Zero)
                this.PowTargetTimespan = consensus.PowTargetTimespan;

            if (consensus.PremineHeight != 0)
                this.PremineHeight = consensus.PremineHeight;

            if (consensus.PremineReward != null)
                this.PremineReward = consensus.PremineReward;

            if (consensus.ProofOfStakeLimit != null)
                this.ProofOfStakeLimit = consensus.ProofOfStakeLimit;

            if (consensus.ProofOfStakeLimitV2 != null)
                this.ProofOfStakeLimitV2 = consensus.ProofOfStakeLimitV2;

            if (consensus.ProofOfStakeReward != null)
                this.ProofOfStakeReward = consensus.ProofOfStakeReward;

            if (consensus.ProofOfWorkReward != null)
                this.ProofOfWorkReward = consensus.ProofOfWorkReward;

            if (consensus.RuleChangeActivationThreshold != 0)
                this.RuleChangeActivationThreshold = consensus.RuleChangeActivationThreshold;

            if (consensus.SubsidyHalvingInterval != 0)
                this.SubsidyHalvingInterval = consensus.SubsidyHalvingInterval;
        }
    }
}