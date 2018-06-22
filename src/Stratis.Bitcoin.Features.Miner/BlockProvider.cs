using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <inheritdoc/>
    public sealed class BlockProvider : IBlockProvider
    {
        /// <summary>Defines how proof of work blocks are built.</summary>
        private readonly PowBlockDefinition powBlockDefinition;

        /// <summary>Defines how proof of stake blocks are built.</summary>
        private readonly PosBlockDefinition posBlockDefinition;

        /// <param name="definitions">A list of block definitions that the builder can utilize.</param>
        public BlockProvider(IEnumerable<BlockDefinition> definitions)
        {
            this.powBlockDefinition = definitions.OfType<PowBlockDefinition>().FirstOrDefault();
            this.posBlockDefinition = definitions.OfType<PosBlockDefinition>().FirstOrDefault();
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            return this.posBlockDefinition.Build(chainTip, script);
        }

        /// <inheritdoc/>
        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            return this.powBlockDefinition.Build(chainTip, script);
        }
    }
}