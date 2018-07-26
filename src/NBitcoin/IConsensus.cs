﻿using System;
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
        Money PremineReward { get; }

        /// <summary>
        /// The height of the block in which the pre-mined coins should be.
        /// Set to 0 when there is no premine.
        /// </summary>
        long PremineHeight { get; }

        /// <summary>
        /// The reward that goes to the miner when a block is mined using proof-of-work.
        /// </summary>
        Money ProofOfWorkReward { get; }

        /// <summary>
        /// The reward that goes to the miner when a block is mined using proof-of-stake.
        /// </summary>
        Money ProofOfStakeReward { get; }

        /// <summary>
        /// Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.
        /// </summary>
        uint MaxReorgLength { get; }

        /// <summary>
        /// The maximum amount of coins in any transaction.
        /// </summary>
        long MaxMoney { get; }

        ConsensusOptions Options { get; set; }
        Consensus.BuriedDeploymentsArray BuriedDeployments { get; }
        Consensus.BIP9DeploymentsArray BIP9Deployments { get; }
        int SubsidyHalvingInterval { get; }
        int MajorityEnforceBlockUpgrade { get; }
        int MajorityRejectBlockOutdated { get; }
        int MajorityWindow { get; }
        uint256 BIP34Hash { get; }
        Target PowLimit { get; }
        TimeSpan PowTargetTimespan { get; }
        TimeSpan PowTargetSpacing { get; }
        bool PowAllowMinDifficultyBlocks { get; }
        bool PowNoRetargeting { get; }
        uint256 HashGenesisBlock { get; }

        /// <summary> The minimum amount of work the best chain should have. </summary>
        uint256 MinimumChainWork { get; }

        long DifficultyAdjustmentInterval { get; }
        int MinerConfirmationWindow { get; set; }
        int RuleChangeActivationThreshold { get; set; }

        /// <summary>
        /// Specify the BIP44 coin type for this network
        /// </summary>
        int CoinType { get; }

        BigInteger ProofOfStakeLimit { get; }
        BigInteger ProofOfStakeLimitV2 { get; }

        /// <summary>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</summary>
        int LastPOWBlock { get; set; }

        /// <summary>
        /// An indicator whether this is a Proof Of Stake network.
        /// </summary>
        bool IsProofOfStake { get; }

        /// <summary>The default hash to use for assuming valid blocks.</summary>
        uint256 DefaultAssumeValid { get; }

        /// <summary>
        /// A factory that enables overloading base types.
        /// </summary>
        ConsensusFactory ConsensusFactory { get; }
    }
}