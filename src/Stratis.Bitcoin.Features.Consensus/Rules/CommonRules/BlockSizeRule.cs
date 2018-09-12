using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    /// <summary>
    /// This rule will validate the block size and weight.
    /// </summary>
    public class BlockSizeRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadBlockWeight">The block weight is higher than the max block weight.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The block length is larger than the allowed max block base size.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The amount of transactions inside the block is higher than the allowed max block base size.</exception>
        /// <exception cref="ConsensusErrors.BadBlockLength">The block does not contain any transactions.</exception>
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            var options = this.Parent.Network.Consensus.Options;

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (this.GetBlockWeight(context.ValidationContext.BlockToValidate, options) > options.MaxBlockWeight)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

            Block block = context.ValidationContext.BlockToValidate;

            // Size limits.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > options.MaxBlockBaseSize) ||
                (GetSize(this.Parent.Network, block, TransactionOptions.None) > options.MaxBlockBaseSize))
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_LEN]");
                ConsensusErrors.BadBlockLength.Throw();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the block weight.
        /// </summary>
        /// <remarks>
        /// This implements the <c>weight = (stripped_size * 4) + witness_size</c> formula, using only serialization with and without witness data.
        /// As witness_size is equal to total_size - stripped_size, this formula is identical to: <c>weight = (stripped_size * 3) + total_size</c>.
        /// </remarks>
        /// <param name="block">Block that we get weight of.</param>
        /// <param name="powOptions">The pow options.</param>
        /// <returns>Block weight.</returns>
        public long GetBlockWeight(Block block, ConsensusOptions powOptions)
        {
            return GetSize(this.Parent.Network, block, TransactionOptions.None) * (powOptions.WitnessScaleFactor - 1) + GetSize(this.Parent.Network, block, TransactionOptions.Witness);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="network">The blockchain network.</param>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        public static int GetSize(Network network, IBitcoinSerializable data, TransactionOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            bms.ConsensusFactory = network.Consensus.ConsensusFactory;
            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }
    }
}