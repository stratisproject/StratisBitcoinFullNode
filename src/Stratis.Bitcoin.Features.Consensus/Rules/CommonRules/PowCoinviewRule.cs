using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// The proof of work coinview update rules. Validates the UTXO set is correctly spent and creating new outputs.
    /// </summary>
    /// <exception cref="ConsensusErrors.BadTransactionMissingInput">Thrown if transaction tries to spend inputs that are missing.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionNonFinal">Thrown if transaction's height or time is lower then provided by SequenceLock for this block.</exception>
    /// <exception cref="ConsensusErrors.BadBlockSigOps">Thrown if signature operation cost is greater then maximum block signature operation cost.</exception>
    /// <exception cref="ConsensusErrors.BadTransactionScriptError">Thrown if not all inputs are valid (no double spends, scripts & sigs, amounts).</exception>
    [ExecutionRule]
    public class PowCoinViewRule : CoinviewRule
    {
        /// <summary>Consensus parameters.</summary>
        private NBitcoin.Consensus consensusParams;

        /// <inheritdoc />
        public override void OnInitialize()
        {
            this.Logger.LogTrace("()");

            this.consensusParams = this.Parent.Network.Consensus;

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public async override Task RunAsync(RuleContext context)
        {
            await base.OnRunAsync(context);
        }

        /// <inheritdoc/>
        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            this.Logger.LogTrace("()");

            Money blockReward = fees + this.GetProofOfWorkReward(height);
            if (block.Transactions[0].TotalOut > blockReward)
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_AMOUNT]");
                ConsensusErrors.BadCoinbaseAmount.Throw();
            }

            this.Logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public override void OnCheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            base.CheckMaturity(coins, spendHeight);
        }

        /// <inheritdoc/>
        public override void OnUpdateCoinView(RuleContext context, Transaction transaction)
        {
            base.UpdateCoinView(context, transaction);
        }

        /// <inheritdoc/>
        public override Money GetProofOfWorkReward(int height)
        {
            int halvings = height / this.consensusParams.SubsidyHalvingInterval;
            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return 0;

            Money subsidy = this.PowConsensusOptions.ProofOfWorkReward;
            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            subsidy >>= halvings;
            return subsidy;
        }

        /// <summary>
        /// Calculates merkle root for witness data.
        /// </summary>
        /// <param name="block">Block which transactions witness data is used for calculation.</param>
        /// <param name="mutated"><c>true</c> if at least one leaf of the merkle tree has the same hash as any subtree. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public uint256 BlockWitnessMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }

        /// <summary>
        /// Calculates merkle root for block's trasnactions.
        /// </summary>
        /// <param name="block">Block which transactions are used for calculation.</param>
        /// <param name="mutated"><c>true</c> if block contains repeating sequences of transactions without affecting the merkle root of a block. Otherwise: <c>false</c>.</param>
        /// <returns>Merkle root.</returns>
        public uint256 BlockMerkleRoot(Block block, out bool mutated)
        {
            var leaves = new List<uint256>(block.Transactions.Count);
            foreach (Transaction tx in block.Transactions)
                leaves.Add(tx.GetHash());

            return BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);
        }
    }
}