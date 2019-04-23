using System.IO;
using NBitcoin;

namespace Stratis.Bitcoin
{
    /// <summary>
    /// TODO: These will move back to BlockSizeRule once has the rules has been migrated.
    /// </summary>
    public static class BlockExtensions
    {
        /// <summary>
        /// Gets the block weight.
        /// </summary>
        /// <remarks>
        /// This implements the <c>weight = (stripped_size * 4) + witness_size</c> formula, using only serialization with and without witness data.
        /// As witness_size is equal to total_size - stripped_size, this formula is identical to: <c>weight = (stripped_size * 3) + total_size</c>.
        /// </remarks>
        /// <param name="block">Block that we get weight of.</param>
        /// <returns>Block weight.</returns>
        public static long GetBlockWeight(this Block block, IConsensus consensus)
        {
            return block.GetSize(TransactionOptions.None, consensus.ConsensusFactory) * (consensus.Options.WitnessScaleFactor - 1) + block.GetSize(TransactionOptions.Witness, consensus.ConsensusFactory);
        }

        /// <summary>
        /// Gets serialized size of <paramref name="data"/> in bytes.
        /// </summary>
        /// <param name="data">Data that we calculate serialized size of.</param>
        /// <param name="options">Serialization options.</param>
        /// <returns>Serialized size of <paramref name="data"/> in bytes.</returns>
        public static int GetSize(this IBitcoinSerializable data, TransactionOptions options, ConsensusFactory consensusFactory)
        {
            var bms = new BitcoinStream(Stream.Null, true)
            {
                TransactionOptions = options,
                ConsensusFactory = consensusFactory
            };

            data.ReadWrite(bms);

            return (int)bms.Counter.WrittenBytes;
        }
    }
}
