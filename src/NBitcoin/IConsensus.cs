using System;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    public interface IConsensus
    {
        /// <summary>
        /// How many blocks should be on top of a coinbase transaction until its outputs are considered spendable.
        /// </summary>
        long CoinbaseMaturity { get; set; }

        /// <summary>
        /// Amount of coins mined when a new network is bootstrapped.
        /// Set to Money.Zero when there is no premine.
        /// </summary>
        Money PremineReward { get; set; }

        /// <summary>
        /// The height of the block in which the pre-mined coins should be.
        /// Set to 0 when there is no premine.
        /// </summary>
        long PremineHeight { get; set; }

        /// <summary>
        /// The reward that goes to the miner when a block is mined using proof-of-work.
        /// </summary>
        Money ProofOfWorkReward { get; set; }

        /// <summary>
        /// The reward that goes to the miner when a block is mined using proof-of-stake.
        /// </summary>
        Money ProofOfStakeReward { get; set; }

        /// <summary>
        /// Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.
        /// </summary>
        uint MaxReorgLength { get; set; }

        /// <summary>
        /// The maximum amount of coins in any transaction.
        /// </summary>
        long MaxMoney { get; set; }

        ConsensusOptions Options { get; set; }
        Consensus.BuriedDeploymentsArray BuriedDeployments { get; set; }
        Consensus.BIP9DeploymentsArray BIP9Deployments { get; set; }
        int SubsidyHalvingInterval { get; set; }
        int MajorityEnforceBlockUpgrade { get; set; }
        int MajorityRejectBlockOutdated { get; set; }
        int MajorityWindow { get; set; }
        uint256 BIP34Hash { get; set; }
        Target PowLimit { get; set; }
        TimeSpan PowTargetTimespan { get; set; }
        TimeSpan PowTargetSpacing { get; set; }
        bool PowAllowMinDifficultyBlocks { get; set; }
        bool PowNoRetargeting { get; set; }
        uint256 HashGenesisBlock { get; set; }

        /// <summary> The minimum amount of work the best chain should have. </summary>
        uint256 MinimumChainWork { get; set; }

        long DifficultyAdjustmentInterval { get; }
        int MinerConfirmationWindow { get; set; }
        int RuleChangeActivationThreshold { get; set; }

        /// <summary>
        /// Specify the BIP44 coin type for this network
        /// </summary>
        int CoinType { get; set; }

        BigInteger ProofOfStakeLimit { get; set; }
        BigInteger ProofOfStakeLimitV2 { get; set; }

        /// <summary>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</summary>
        int LastPOWBlock { get; set; }

        /// <summary>
        /// An indicator whether this is a Proof Of Stake network.
        /// </summary>
        bool IsProofOfStake { get; set; }

        /// <summary>The default hash to use for assuming valid blocks.</summary>
        uint256 DefaultAssumeValid { get; set; }

        /// <summary>
        /// A factory that enables overloading base types.
        /// </summary>
        ConsensusFactory ConsensusFactory { get; set; }
    }
}