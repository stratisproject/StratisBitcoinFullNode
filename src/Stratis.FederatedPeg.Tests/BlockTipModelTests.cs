using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Stratis.FederatedPeg.Features.FederationGateway.Interfaces;
using Stratis.FederatedPeg.Features.FederationGateway.Models;
using Stratis.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    public class BlockTipModelTests
    {
        public static IBlockTip PrepareBlockTip()
        {
            var blockHash = TestingValues.GetUint256();
            var blockHeight = TestingValues.GetPositiveInt();

            var blockTip = new BlockTipModel(blockHash, blockHeight);
            return blockTip;
        }

        [Fact]
        public void ShouldSerialiseAsJson()
        {
            var blockTip = PrepareBlockTip();
            var asJson = blockTip.ToString();

            var reconverted = JsonConvert.DeserializeObject<BlockTipModel>(asJson);

            reconverted.Hash.Should().Be(blockTip.Hash);
            reconverted.Height.Should().Be(blockTip.Height);
        }
    }
}
