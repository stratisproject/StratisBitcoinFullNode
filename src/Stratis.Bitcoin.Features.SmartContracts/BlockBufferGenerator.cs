using Stratis.Bitcoin.Features.Miner;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class BlockBufferGenerator : IBlockBufferGenerator
    {
        public BlockDefinitionOptions GetOptionsWithBuffer(BlockDefinitionOptions options)
        {
            uint percentageBuffer = 5; // For 1MB blocks, 50 KB reserved for generated transactions / txouts
            uint percentageToBuild = 100 - percentageBuffer;
            return new BlockDefinitionOptions(options.BlockMaxSize * percentageToBuild / 100, options.BlockMaxWeight * percentageToBuild / 100);
        }
    }
}
