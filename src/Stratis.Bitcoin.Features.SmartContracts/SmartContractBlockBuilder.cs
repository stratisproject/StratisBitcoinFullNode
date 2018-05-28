using NBitcoin;
using Stratis.Bitcoin.Mining;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractBlockBuilder : IBlockBuilder
    {
        private readonly SmartContractBlockDefinition blockAssembler;

        public SmartContractBlockBuilder(SmartContractBlockDefinition blockAssembler)
        {
            this.blockAssembler = blockAssembler;
        }

        public BlockTemplate Build(BlockBuilderMode blockBuildMode, ChainedHeader chainTip, Script script)
        {
            return this.blockAssembler.Build(chainTip, script);
        }
    }
}