using System;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

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
        /// <returns><c>true</c> if the coin stake satisfies the weighted target, otherwise <c>false</c>.</returns>
        bool CheckKernel(PosRuleContext context, ChainedHeader prevChainedHeader, uint headerBits, long transactionTime, OutPoint prevout);

        /// <summary>
        /// Checks that the stake kernel hash satisfies the target difficulty.
        /// </summary>
        /// <param name="context">Staking context.</param>
        /// <param name="headerBits">Chained block's header bits, which define the difficulty target.</param>
        /// <param name="prevStakeModifier">Previous staked block modifier.</param>
        /// <param name="stakingCoins">Coins that participate in staking.</param>
        /// <param name="prevout">Information about transaction id and index.</param>
        /// <param name="transactionTime">Transaction time.</param>
        /// <remarks>
        /// Coinstake must meet hash target according to the protocol:
        /// kernel (input 0) must meet the formula
        /// <c>hash(stakeModifierV2 + stakingCoins.Time + prevout.Hash + prevout.N + transactionTime) &lt; target * weight</c>.
        /// This ensures that the chance of getting a coinstake is proportional to the amount of coins one owns.
        /// <para>
        /// The reason this hash is chosen is the following:
        /// <list type="number">
        /// <item><paramref name="prevStakeModifier"/>: Scrambles computation to make it very difficult to precompute future proof-of-stake.</item>
        /// <item><paramref name="stakingCoins.Time"/>: Time of the coinstake UTXO. Slightly scrambles computation.</item>
        /// <item><paramref name="prevout.Hash"/> Hash of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.</item>
        /// <item><paramref name="prevout.N"/>: Output number of stakingCoins UTXO, to reduce the chance of nodes generating coinstake at the same time.</item>
        /// <item><paramref name="transactionTime"/>: Timestamp of the coinstake transaction.</item>
        /// </list>
        /// Block or transaction tx hash should not be used here as they can be generated in vast
        /// quantities so as to generate blocks faster, degrading the system back into a proof-of-work situation.
        /// </para>
        /// </remarks>
        /// <exception cref="ConsensusErrors.StakeTimeViolation">Thrown in case transaction time is lower than it's own UTXO timestamp.</exception>
        /// <exception cref="ConsensusErrors.StakeHashInvalidTarget">Thrown in case PoS hash doesn't meet target protocol.</exception>
        /// <returns><c>true</c> if the coin stake satisfies the weighted target, otherwise <c>false</c>.</returns>
        bool CheckStakeKernelHash(PosRuleContext context, uint headerBits, uint256 prevStakeModifier, UnspentOutputs stakingCoins, OutPoint prevout, uint transactionTime);

        /// <summary>
        /// Checks if provided transaction is a valid coinstake transaction.
        /// </summary>
        /// <param name="context">Staking context.</param>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <param name="prevBlockStake">Information about previous staked block.</param>
        /// <param name="transaction">The transaction.</param>
        /// <param name="headerBits">Chained block's header bits, which define the difficulty target.</param>
        void CheckProofOfStake(PosRuleContext context, ChainedHeader prevChainedHeader, BlockStake prevBlockStake, Transaction transaction, uint headerBits);

        /// <summary>
        /// Computes stake modifier.
        /// </summary>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <param name="prevStakeModifier">Previous PoS block StakeModifier.</param>
        /// <param name="kernel">The PoS kernel.</param>
        /// <returns>Stake modifier.</returns>
        uint256 ComputeStakeModifierV2(ChainedHeader prevChainedHeader, uint256 prevStakeModifier, uint256 kernel);

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
        Target GetNextTargetRequired(IStakeChain stakeChain, ChainedHeader chainedHeader, IConsensus consensus, bool proofOfStake);

        /// <summary>
        /// Calculates the difficulty between two block time spans.
        /// </summary>
        /// <param name="firstBlockTime">The time of the first block.</param>
        /// <param name="firstBlockTarget">The target of the first block.</param>
        /// <param name="secondBlockTime">The block time of the second block.</param>
        /// <param name="targetLimit">The upper limit of what the target can be.</param>
        /// <returns>The new difficulty target as the outcome of the previous two blocks.</returns>
        /// <remarks>
        /// The calculation of the next target is based on the last target value and the block time (aka spacing) of <paramref name="firstBlockTime"/>
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
        Target CalculateRetarget(uint firstBlockTime, Target firstBlockTarget, uint secondBlockTime, BigInteger targetLimit);

        /// <summary>
        /// Verifies transaction's signature.
        /// </summary>
        /// <param name="coin">UTXO that is spent in the transaction.</param>
        /// <param name="txTo">Transaction.</param>
        /// <param name="txToInN">Index of the transaction's input.</param>
        /// <param name="flagScriptVerify">Script verification flags.</param>
        /// <returns><c>true</c> if signature is valid.</returns>
        bool VerifySignature(UnspentOutputs coin, Transaction txTo, int txToInN, ScriptVerify flagScriptVerify);

        /// <summary>
        /// Returns <c>true</c> if provided coins were confirmed in less than <paramref name="targetDepth"/> number of blocks.
        /// </summary>
        /// <param name="coins">Coins to check confirmation depth for.</param>
        /// <param name="referenceChainedHeader">Chained block from which we are counting the depth.</param>
        /// <param name="targetDepth">The target depth.</param>
        /// <returns><c>true</c> if the coins were spent within N blocks from <see cref="referenceChainedHeader"/>, <c>false</c> otherwise.</returns>
        bool IsConfirmedInNPrevBlocks(UnspentOutputs coins, ChainedHeader referenceChainedHeader, long targetDepth);

        /// <summary>
        /// Gets the required target depth according to the previous chained header and the consensus options.
        /// </summary>
        /// <param name="prevChainedHeader">Previous chained block.</param>
        /// <returns>A value indicating the required target depth in number of blocks.</returns>
        long GetTargetDepthRequired(ChainedHeader prevChainedHeader);

        /// <summary>
        /// Validates the POS Block Signature.
        /// </summary>
        /// <param name="signature">The signature to validate.</param>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="coinStake">The coinstake transaction.</param>
        /// <returns><c>True</c> if passes validation, and <c>false</c> otherwise.</returns>
        bool CheckStakeSignature(BlockSignature signature, uint256 blockHash, Transaction coinStake);
    }
}