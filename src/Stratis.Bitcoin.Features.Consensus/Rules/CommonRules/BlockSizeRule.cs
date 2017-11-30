using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class BlockSizeRule : ConsensusRule
    {
        public override bool CanSkipValidation => true;

        public override Task RunAsync(ContextInformation context)
        {
            var options = context.Consensus.Option<PowConsensusOptions>();

            // After the coinbase witness nonce and commitment are verified,
            // we can check if the block weight passes (before we've checked the
            // coinbase witness, it would be possible for the weight to be too
            // large by filling up the coinbase witness, which doesn't change
            // the block hash, so we couldn't mark the block as permanently
            // failed).
            if (GetBlockWeight(context.BlockValidationContext.Block, options) > options.MaxBlockWeight)
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_WEIGHT]");
                ConsensusErrors.BadBlockWeight.Throw();
            }

            Block block = context.BlockValidationContext.Block;

            // Size limits.
            if ((block.Transactions.Count == 0) || (block.Transactions.Count > options.MaxBlockBaseSize) || (GetSize(block, TransactionOptions.None) > options.MaxBlockBaseSize))
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_LEN]");
                ConsensusErrors.BadBlockLength.Throw();
            }

            return Task.CompletedTask;
        }

        public static long GetBlockWeight(Block block, PowConsensusOptions options)
        {
            // This implements the weight = (stripped_size * 4) + witness_size formula,
            // using only serialization with and without witness data. As witness_size
            // is equal to total_size - stripped_size, this formula is identical to:
            // weight = (stripped_size * 3) + total_size.
            return GetSize(block, TransactionOptions.None) * (options.WitnessScaleFactor - 1) + GetSize(block, TransactionOptions.Witness);
        }

        public static int GetSize(IBitcoinSerializable data, TransactionOptions options)
        {
            var bms = new BitcoinStream(Stream.Null, true);
            bms.TransactionOptions = options;
            data.ReadWrite(bms);
            return (int)bms.Counter.WrittenBytes;
        }
    }
}