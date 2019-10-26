using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Semi- immutable settings to be used by <see cref="BlockDefinition"/>.
    /// </summary>
    public sealed class BlockDefinitionOptions
    {
        /// <summary>Minimum block size in bytes. Could be set per network in future.</summary>
        private const uint MinBlockSize = 1000;

        /// <summary>Maximum block weight (in weight units) for the blocks created by miner.</summary>
        public uint BlockMaxWeight { get; private set; }

        /// <summary>Maximum block size (in bytes) for the blocks created by miner.</summary>
        public uint BlockMaxSize { get; private set; }

        /// <summary>Minimum fee rate for transactions to be included in blocks created by miner.</summary>
        public FeeRate BlockMinFeeRate { get; private set; }

        public BlockDefinitionOptions(uint blockMaxWeight, uint blockMaxSize)
        {
            this.BlockMaxWeight = blockMaxWeight;
            this.BlockMaxSize = blockMaxSize;
            this.BlockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee); // TODO: Where should this be set, really?
        }

        /// <summary>
        /// Restrict the options to within those allowed by network consensus rules.
        /// If set values are outside those allowed by consensus, set to nearest allowed value (minimum or maximum).
        /// </summary>
        public BlockDefinitionOptions RestrictForNetwork(Network network)
        {
            uint minAllowedBlockWeight = MinBlockSize * (uint) network.Consensus.Options.WitnessScaleFactor;
            this.BlockMaxWeight = Math.Max(minAllowedBlockWeight, Math.Min(network.Consensus.Options.MaxBlockWeight, this.BlockMaxWeight));
            this.BlockMaxSize = Math.Max(MinBlockSize, Math.Min(network.Consensus.Options.MaxBlockSerializedSize, this.BlockMaxSize));

            return this;
        }
    }
}