using FluentAssertions;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class BlockTipModelTests
    {
        public static IBlockTip PrepareBlockTip()
        {
            uint256 blockHash = TestingValues.GetUint256();
            int blockHeight = TestingValues.GetPositiveInt();
            int matureConfirmation = TestingValues.GetPositiveInt();

            var blockTip = new BlockTipModel(blockHash, blockHeight, matureConfirmation);
            return blockTip;
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void ShouldSerialiseAsJson()
        {
            IBlockTip blockTip = PrepareBlockTip();
            string asJson = blockTip.ToString();

            var reconverted = JsonConvert.DeserializeObject<BlockTipModel>(asJson);

            reconverted.Hash.Should().Be(blockTip.Hash);
            reconverted.Height.Should().Be(blockTip.Height);
            reconverted.MatureConfirmations.Should().Be(blockTip.MatureConfirmations);
        }
    }
}
