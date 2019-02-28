using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Rules;

namespace NBitcoin
{
    public interface IConsensus
    {
        /// <summary>
        /// How many blocks should be on top of a block that includes a coinbase transaction until its outputs are considered spendable.
        /// </summary>
        long CoinbaseMaturity { get; set; }

        /// <summary>
        /// Amount of coins mined when a new network is bootstrapped.
        /// Set to <see cref="Money.Zero"/> when there is no premine.
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

        BuriedDeploymentsArray BuriedDeployments { get; }

        IBIP9DeploymentsArray BIP9Deployments { get; }

        int SubsidyHalvingInterval { get; }

        int MajorityEnforceBlockUpgrade { get; }

        int MajorityRejectBlockOutdated { get; }

        int MajorityWindow { get; }

        uint256 BIP34Hash { get; }

        Target PowLimit { get; }

        TimeSpan PowTargetTimespan { get; }

        TimeSpan PowTargetSpacing { get; }

        bool PowAllowMinDifficultyBlocks { get; }

        /// <summary>
        /// If <c>true</c> disables checking the next block's difficulty (work required) target on a Proof-Of-Stake network.
        /// <para>
        /// This can be used in tests to enable fast mining of blocks.
        /// </para>
        /// </summary>
        bool PosNoRetargeting { get; }

        /// <summary>
        /// If <c>true</c> disables checking the next block's difficulty (work required) target on a Proof-Of-Work network.
        /// <para>
        /// This can be used in tests to enable fast mining of blocks.
        /// </para>
        /// </summary>
        bool PowNoRetargeting { get; }

        uint256 HashGenesisBlock { get; }

        /// <summary> The minimum amount of work the best chain should have. </summary>
        uint256 MinimumChainWork { get; }

        int MinerConfirmationWindow { get; set; }

        int RuleChangeActivationThreshold { get; set; }

        /// <summary>
        /// Specify the BIP44 coin type for this network.
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

        /// <summary>Group of rules that are used during block header validation specific to the given network.</summary>
        List<IHeaderValidationConsensusRule> HeaderValidationRules { get; set; }

        /// <summary>Group of rules that are used during block integrity validation specific to the given network.</summary>
        List<IIntegrityValidationConsensusRule> IntegrityValidationRules { get; set; }

        /// <summary>Group of rules that are used during partial block validation specific to the given network.</summary>
        List<IPartialValidationConsensusRule> PartialValidationRules { get; set; }

        /// <summary>Group of rules that are used during full validation (connection of a new block) specific to the given network.</summary>
        List<IFullValidationConsensusRule> FullValidationRules { get; set; }
    }
}