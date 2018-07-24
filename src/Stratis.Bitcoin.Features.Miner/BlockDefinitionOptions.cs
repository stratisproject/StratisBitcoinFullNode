using NBitcoin;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Immutable settings to be used by <see cref="BlockDefinition"/>.
    /// </summary>
    public sealed class BlockDefinitionOptions
    {
        public long BlockMaxWeight { get; }

        public long BlockMaxSize { get; }

        public FeeRate BlockMinFeeRate { get; }

        /// <summary>
        /// Use the defaults from a network. No user settings.
        /// </summary>
        public BlockDefinitionOptions(Network network)
        {
            this.BlockMaxWeight = network.Consensus.Options.MaxBlockWeight;
            this.BlockMaxSize = network.Consensus.Options.MaxBlockBaseSize;
            this.BlockMinFeeRate = new FeeRate(PowMining.DefaultBlockMinTxFee); // TODO: Where should this be set, really? Is it per Network? For now just always use a default.
        }

        public BlockDefinitionOptions(long blockMaxWeight, long blockMaxSize, FeeRate blockMinFeeRate)
        {
            this.BlockMaxWeight = blockMaxWeight;
            this.BlockMaxSize = blockMaxSize;
            this.BlockMinFeeRate = blockMinFeeRate;
        }

    }
}