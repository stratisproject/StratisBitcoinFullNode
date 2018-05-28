using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.Miner
{
    public sealed class BlockBuilder : IBlockBuilder
    {
        private readonly BlockDefinitionProofOfWork powBlockAssembler;
        private readonly BlockDefinitionProofOfStake posBlockAssembler;

        /// <summary>
        /// This is constructor is called by dependency injection.
        /// </summary>
        /// <param name="assemblers">A list of block definitions that the builder can utilize.</param>
        public BlockBuilder(IEnumerable<BlockDefinition> assemblers)
        {
            this.powBlockAssembler = assemblers.FirstOrDefault(a => a.GetType() == typeof(BlockDefinitionProofOfWork)) as BlockDefinitionProofOfWork;
            this.posBlockAssembler = assemblers.FirstOrDefault(a => a.GetType() == typeof(BlockDefinitionProofOfStake)) as BlockDefinitionProofOfStake;
        }

        public BlockTemplate Build(BlockBuilderMode mode, ChainedHeader chainTip, Script script)
        {
            if (mode == BlockBuilderMode.Mining)
                return this.powBlockAssembler.Build(chainTip, script);
            else
                return this.posBlockAssembler.Build(chainTip, script);
        }
    }
}