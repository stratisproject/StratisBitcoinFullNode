using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Interfaces
{
    /// <summary>
    /// Provides functionality for verifying validity of PoW block.
    /// </summary>
    public interface IPowConsensusValidator
    {
        /// <summary>Consensus options.</summary>
        PowConsensusOptions ConsensusOptions { get; }

        /// <summary>Consensus parameters.</summary>
        NBitcoin.Consensus ConsensusParams { get; }

        /// <summary>Keeps track of how much time different actions took to execute and how many times they were executed.</summary>
        ConsensusPerformanceCounter PerformanceCounter { get; }

        /// <summary>
        /// Calculates merkle root for block's trasnactions.
        /// </summary>
        /// <param name="block">Block which transactions are used for calculation.</param>
        /// <param name="mutated"><c>true</c> if block contains repeating sequences of transactions without affecting the merkle root of a block. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        uint256 BlockMerkleRoot(Block block, out bool mutated);

        /// <summary>
        /// Calculates merkle root for witness data.
        /// </summary>
        /// <param name="block">Block which transactions witness data is used for calculation.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        uint256 BlockWitnessMerkleRoot(Block block, out bool mutated);
    
        /// <summary>
        /// Checks that transaction's inputs are valid.
        /// </summary>
        /// <param name="transaction">Transaction to check.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="spendHeight">Height at which we are spending coins.</param>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction's inputs are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInputValueOutOfRange">Thrown if input value is out of range.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionInBelowOut">Thrown if transaction inputs are less then outputs.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNegativeFee">Thrown if fees sum is negative.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionFeeOutOfRange">Thrown if fees value is out of range.</exception>
        void CheckInputs(Transaction transaction, UnspentOutputSet inputs, int spendHeight);

        /// <summary>
        /// Validates the UTXO set is correctly spent.
        /// </summary>
        /// <param name="context">Context that contains variety of information regarding blocks validation and execution.</param>
        /// <param name="taskScheduler">Task scheduler for creating tasks that would check validity of each transaction input.</param>
        /// <exception cref="ConsensusErrors.BadTransactionBIP30">Thrown if block contain transactions which 'overwrite' older transactions.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction tries to spend inputs that are missing.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if transaction's height or time is lower then provided by SequenceLock for this block.</exception>
        /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if signature operation cost is greater then maximum block signature operation cost.</exception>
        /// <exception cref="ConsensusErrors.BadTransactionScriptError">Thrown if not all inputs are valid (no double spends, scripts & sigs, amounts).</exception>
        void ExecuteBlock(RuleContext context, TaskScheduler taskScheduler = null);

        /// <summary>
        /// Gets the block weight.
        /// </summary>
        /// <remarks>
        /// This implements the <c>weight = (stripped_size * 4) + witness_size</c> formula, using only serialization with and without witness data.
        /// As witness_size is equal to total_size - stripped_size, this formula is identical to: <c>weight = (stripped_size * 3) + total_size</c>.
        /// </remarks>
        /// <param name="block">Block that we get weight of.</param>
        /// <returns>Block weight.</returns>
        long GetBlockWeight(Block block);

        /// <summary>
        /// Gets the proof of work reward amount for the block at provided height.
        /// </summary>
        /// <param name="height">Height of the block that we're calculating the reward for.</param>
        /// <returns>Reward amount.</returns>
        Money GetProofOfWorkReward(int height);

        /// <summary>
        /// Calculates total signature operation cost of a transaction.
        /// </summary>
        /// <param name="transaction">Transaction for which we are computing the cost.</param>
        /// <param name="inputs">Map of previous transactions that have outputs we're spending.</param>
        /// <param name="flags">Script verification flags.</param>
        /// <returns>Signature operation cost for all transaction's inputs.</returns>
        long GetTransactionSignatureOperationCost(Transaction transaction, UnspentOutputSet inputs, DeploymentFlags flags);
    }
}