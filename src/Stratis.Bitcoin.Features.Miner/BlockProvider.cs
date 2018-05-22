using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.Miner
{
    public sealed class BlockProvider : IBlockProvider
    {
        private readonly PowBlockDefinition powBlockDefinition;
        private readonly PosBlockDefinition posBlockDefinition;

        /// <summary>
        /// This is constructor is called by dependency injection.
        /// </summary>
        /// <param name="definitions">A list of block definitions that the builder can utilize.</param>
        public BlockProvider(IEnumerable<BlockDefinition> definitions)
        {
            this.powBlockDefinition = definitions.FirstOrDefault(a => a.GetType() == typeof(PowBlockDefinition)) as PowBlockDefinition;
            this.posBlockDefinition = definitions.FirstOrDefault(a => a.GetType() == typeof(PosBlockDefinition)) as PosBlockDefinition;
        }

        public BlockTemplate BuildPosBlock(ChainedHeader chainTip, Script script)
        {
            return this.posBlockDefinition.Build(chainTip, script);
        }

        public BlockTemplate BuildPowBlock(ChainedHeader chainTip, Script script)
        {
            return this.powBlockDefinition.Build(chainTip, script);
        }
    }
}