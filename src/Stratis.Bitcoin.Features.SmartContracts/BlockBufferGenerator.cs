using Stratis.Bitcoin.Features.Miner;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class BlockBufferGenerator : IBlockBufferGenerator
    {
        public BlockDefinitionOptions GetOptionsWithBuffer(BlockDefinitionOptions options)
        {
            uint percentageToBuild = 95; // For 1MB blocks, 50 KB reserved for generated transactions / txouts
            return new BlockDefinitionOptions(options.BlockMaxSize * percentageToBuild / 100, options.BlockMaxWeight * percentageToBuild / 100);
        }
    }
}
