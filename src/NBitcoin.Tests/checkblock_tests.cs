using System.IO;
using NBitcoin.DataEncoders;
using Xunit;

namespace NBitcoin.Tests
{
    public class checkblock_tests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCalculateMerkleRoot()
        {
            Block block = Networks.Main.CreateBlock();
            block.ReadWrite(Encoders.Hex.DecodeData(File.ReadAllText(TestDataLocations.GetFileFromDataFolder("block169482.txt"))), Networks.Main.Consensus.ConsensusFactory);
            Assert.Equal(block.Header.HashMerkleRoot, block.GetMerkleRoot().Hash);
        }
    }
}