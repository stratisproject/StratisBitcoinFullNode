using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class BlockBufferGeneratorTests
    {
        private readonly BlockBufferGenerator bufferGen;

        public BlockBufferGeneratorTests()
        {
            this.bufferGen = new BlockBufferGenerator();
        }

        [Fact]
        public void Buffer_50Kb_For_1MB_BlockSize()
        {
            BlockDefinitionOptions optionsFromNetwork = new MinerSettings(new NodeSettings(new SmartContractsTest())).BlockDefinitionOptions;
            BlockDefinitionOptions newOptions = this.bufferGen.GetOptionsWithBuffer(optionsFromNetwork);

            Assert.Equal((uint) 950_000, newOptions.BlockMaxWeight);
            Assert.Equal((uint) 950_000, newOptions.BlockMaxSize);
        }
    }
}
