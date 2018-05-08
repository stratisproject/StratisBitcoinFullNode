using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Interfaces
{
    /// <summary>
    /// Provides functionality for checking validity of PoS blocks.
    /// See <see cref="Stratis.Bitcoin.Features.Miner.PosMinting"/> for more information about PoS solutions.
    /// </summary>
    public interface IStakeValidator
    {
        /// <summary>
        /// Checks that UTXO is valid for staking and then checks kernel hash.
        /// </summary>
        /// <param name="context">Staking context.</param>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <param name="headerBits">Chained block's header bits, which define the difficulty target.</param>
        /// <param name="transactionTime">Transaction time.</param>
        /// <param name="prevout">Information about transaction id and index.</param>
        /// <param name="prevBlockTime">The previous block time.</param>
        void CheckKernel(ContextStakeInformation context, ChainedHeader prevChainedHeader, uint headerBits, long transactionTime, OutPoint prevout);

        /// <summary>
        /// Checks if provided transaction is a valid coinstake transaction.
        /// </summary>
        /// <param name="context">Staking context.</param>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <param name="prevBlockStake">Information about previous staked block.</param>
        /// <param name="transaction">The transaction.</param>
        /// <param name="headerBits">Chained block's header bits, which define the difficulty target.</param>
        void CheckProofOfStake(ContextStakeInformation context, ChainedHeader prevChainedHeader, BlockStake prevBlockStake, Transaction transaction, uint headerBits);

        /// <summary>
        /// Computes stake modifier.
        /// </summary>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <param name="blockStakePrev">Previous PoS block.</param>
        /// <param name="kernel">The PoS kernel.</param>
        /// <returns>Stake modifier.</returns>
        uint256 ComputeStakeModifierV2(ChainedHeader prevChainedHeader, BlockStake blockStakePrev, uint256 kernel);

        /// <summary>
        /// Gets the last block in the chain that was generated using
        /// PoS if <paramref name="proofOfStake"/> is <c>true</c> or PoW if <paramref name="proofOfStake"/> is <c>false</c>.
        /// </summary>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="startChainedHeader">Block that we start from. Only blocks before that one will be checked.</param>
        /// <param name="proofOfStake">Specifies what kind of block we are looking for: <c>true</c> for PoS or <c>false</c> for PoW.</param>
        /// <returns>Last block in the chain that satisfies provided requirements.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="startChainedHeader"/> is <c>null</c>.</exception>
        ChainedHeader GetLastPowPosChainedBlock(IStakeChain stakeChain, ChainedHeader startChainedHeader, bool proofOfStake);

        /// <summary>
        /// Calculates the difficulty target for the next block.
        /// </summary>
        /// <param name="stakeChain">Database of stake related data for the current blockchain.</param>
        /// <param name="chainedHeader">Block header for which to calculate the target difficulty.</param>
        /// <param name="consensus">Consensus rules for the current network.</param>
        /// <param name="proofOfStake"><c>true</c> for calculation of PoS difficulty target, <c>false</c> for calculation of PoW difficulty target.</param>
        /// <returns>The difficulty target for the next block after <paramref name="chainedHeader"/>.</returns>
        /// <remarks>
        /// The calculation of the next target is based on the last target value and the block time (aka spacing) of <paramref name="chainedHeader"/>
        /// (i.e. difference in time stamp of this block and its immediate predecessor). The target changes every block and it is adjusted
        /// down (i.e. towards harder to reach, or more difficult) if the time to mine last block was lower than the target block time.
        /// And it is adjusted up if it took longer than the target block time. The adjustments are done in a way the target is moving towards
        /// the target-spacing (expected block time) exponentially, so even a big change in the mining power on the network will be fixed by retargeting relatively quickly.
        /// <para>
        /// Over <see cref="RetargetIntervalMinutes"/> minutes there are certain number (say <c>N</c>) of blocks expected to be mined if the target block time
        /// of <see cref="TargetSpacingSeconds"/> was reached every time. Then the next target is calculated as follows:</para>
        /// <code>
        /// NewTarget = PrevTarget * ((N - 1) * TargetSpacingSeconds + 2 * LastBlockTime) / ((N + 1) * TargetSpacingSeconds)
        /// </code>
        /// <para>
        /// Which basically says that the block time of the last block is counted twice instead of two optimal block times.
        /// And the <c>N</c> determines how strongly will the deviation of the last block time affect the difficulty.
        /// </para>
        /// </remarks>
        Target GetNextTargetRequired(IStakeChain stakeChain, ChainedHeader chainedHeader, NBitcoin.Consensus consensus, bool proofOfStake);
    }
}