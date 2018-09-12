using Stratis.Bitcoin.Features.Miner;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface IBlockBufferGenerator
    {
        BlockDefinitionOptions GetOptionsWithBuffer(BlockDefinitionOptions options);
    }
}
