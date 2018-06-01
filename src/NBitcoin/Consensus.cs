using System;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    public enum BuriedDeployments
    {
        /// <summary>
        /// Height in coinbase.
        /// </summary>
        BIP34,

        /// <summary>
        /// Height in OP_CLTV.
        /// </summary>
        BIP65,

        /// <summary>
        /// Strict DER signature.
        /// </summary>
        BIP66
    }

    public class Consensus
    {
        /// <summary>
        /// An extension to <see cref="Consensus"/> to enable additional options to the consensus data.
        /// </summary>
        public class ConsensusOptions
        {
        }

        public ConsensusOptions Options { get; set; }

        public class BuriedDeploymentsArray
        {
            private readonly int[] heights;

            public BuriedDeploymentsArray()
            {
                this.heights = new int[Enum.GetValues(typeof(BuriedDeployments)).Length];
            }

            public int this[BuriedDeployments index]
            {
                get => this.heights[(int)index];
                set => this.heights[(int)index] = value;
            }
        }

        public class BIP9DeploymentsArray
        {
            private readonly BIP9DeploymentsParameters[] parameters;

            public BIP9DeploymentsArray()
            {
                this.parameters = new BIP9DeploymentsParameters[Enum.GetValues(typeof(BIP9Deployments)).Length];
            }

            public BIP9DeploymentsParameters this[BIP9Deployments index]
            {
                get => this.parameters[(int)index];
                set => this.parameters[(int)index] = value;
            }
        }

        public Consensus()
        {
            this.BuriedDeployments = new BuriedDeploymentsArray();
            this.BIP9Deployments = new BIP9DeploymentsArray();

            this.ConsensusFactory = new ConsensusFactory()
            {
                Consensus = this
            };
        }

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

        /// <summary> The minimum amount of work the best chain should have. </summary>
        public uint256 MinimumChainWork { get; set; }

        public long DifficultyAdjustmentInterval
        {
            get { return ((long)this.PowTargetTimespan.TotalSeconds / (long)this.PowTargetSpacing.TotalSeconds); }
        }

        public int MinerConfirmationWindow { get; set; }

        public int RuleChangeActivationThreshold { get; set; }

        /// <summary>
        /// Specify the BIP44 coin type for this network
        /// </summary>
        public int CoinType { get; set; }

        public BigInteger ProofOfStakeLimit { get; set; }

        public BigInteger ProofOfStakeLimitV2 { get; set; }

        /// <summary>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</summary>
        public int LastPOWBlock { get; set; }

        /// <summary>
        /// An indicator whether this is a Proof Of Stake network.
        /// </summary>
        public bool IsProofOfStake { get; set; }

        /// <summary>The default hash to use for assuming valid blocks.</summary>
        public uint256 DefaultAssumeValid { get; set; }

        /// <summary>
        /// A factory that enables overloading base types.
        /// </summary>
        public ConsensusFactory ConsensusFactory { get; set; }

        public virtual Consensus Clone()
        {
            return new Consensus
            {
                BIP34Hash = this.BIP34Hash,
                HashGenesisBlock = this.HashGenesisBlock,
                MajorityEnforceBlockUpgrade = this.MajorityEnforceBlockUpgrade,
                MajorityRejectBlockOutdated = this.MajorityRejectBlockOutdated,
                MajorityWindow = this.MajorityWindow,
                MinerConfirmationWindow = this.MinerConfirmationWindow,
                PowAllowMinDifficultyBlocks = this.PowAllowMinDifficultyBlocks,
                PowLimit = this.PowLimit,
                PowNoRetargeting = this.PowNoRetargeting,
                PowTargetSpacing = this.PowTargetSpacing,
                PowTargetTimespan = this.PowTargetTimespan,
                RuleChangeActivationThreshold = this.RuleChangeActivationThreshold,
                SubsidyHalvingInterval = this.SubsidyHalvingInterval,
                MinimumChainWork = this.MinimumChainWork,
                CoinType = this.CoinType,
                IsProofOfStake = this.IsProofOfStake,
                LastPOWBlock = this.LastPOWBlock,
                ProofOfStakeLimit = this.ProofOfStakeLimit,
                ProofOfStakeLimitV2 = this.ProofOfStakeLimitV2,
                DefaultAssumeValid = this.DefaultAssumeValid,
                ConsensusFactory = this.ConsensusFactory,
            };
        }
    }
}
