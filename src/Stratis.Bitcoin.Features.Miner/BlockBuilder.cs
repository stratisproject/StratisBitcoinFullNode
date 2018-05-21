using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.Miner
{
    public sealed class BlockBuilder : IBlockBuilder
    {
        private readonly PowBlockAssembler powBlockAssembler;
        private readonly PosBlockAssembler posBlockAssembler;

        /// <summary>
        /// This is constructor is called by dependency injection.
        /// </summary>
        /// <param name="assemblers">A list of block definitions that the builder can utilize.</param>
        public BlockBuilder(IEnumerable<BlockAssembler> assemblers)
        {
            this.powBlockAssembler = assemblers.FirstOrDefault(a => a.GetType() == typeof(PowBlockAssembler)) as PowBlockAssembler;
            this.posBlockAssembler = assemblers.FirstOrDefault(a => a.GetType() == typeof(PosBlockAssembler)) as PosBlockAssembler;
        }

        public BlockBuilder(PowBlockAssembler powBlockAssembler)
        {
            this.powBlockAssembler = powBlockAssembler;
        }

        public BlockBuilder(PosBlockAssembler posBlockAssembler)
        {
            this.posBlockAssembler = posBlockAssembler;
        }

        public BlockTemplate Build(BlockBuilderMode mode, ChainedHeader chainTip, Script script)
        {
            if (mode == BlockBuilderMode.Staking)
                return this.posBlockAssembler.Build(chainTip, script);
            else
                return this.powBlockAssembler.Build(chainTip, script);
        }
    }
}