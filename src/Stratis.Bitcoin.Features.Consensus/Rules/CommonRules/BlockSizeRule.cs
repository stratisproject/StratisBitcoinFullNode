using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class BlockSizeRule : ConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            var options = context.Consensus.Option<PowConsensusOptions>();

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (this.GetBlockWeight(context.BlockValidationContext.Block, options, context.Consensus.NetworkOptions) > options.MaxBlockWeight)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

            Block block = context.BlockValidationContext.Block;

            // Size limits.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > options.MaxBlockBaseSize) || 
                (GetSize(block, context.Consensus.NetworkOptions & ~NetworkOptions.All) > options.MaxBlockBaseSize))
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
        /// <param name="options">Options for POW networks.</param>
        /// <returns>Block weight.</returns>
        public long GetBlockWeight(Block block, PowConsensusOptions powOptions, NetworkOptions options)
        {
            return GetSize(block, options & ~NetworkOptions.Witness) * (powOptions.WitnessScaleFactor - 1) + GetSize(block, options);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        public static int GetSize(IBitcoinSerializable data, NetworkOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }
    }
}