using System;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    /// <summary>
    /// Used to create a mutable version of the <see cref="Consensus"/> object to be used
    /// during configuration
    /// </summary>
    public class MutableConsensus : IConsensus
    {
        public MutableConsensus()
        {
            this.BuriedDeployments = new BuriedDeploymentsArray();
            this.BIP9Deployments = new BIP9DeploymentsArray();
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
    }
}