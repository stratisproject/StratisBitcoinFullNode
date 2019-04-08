using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class MerkleRootComputationTest
    {
        [Fact]
        public void MerkleRootComputationNotMutated()
        {
            var leaves = new List<uint256>()
            {
                new uint256("281f5acb40a15640bc48b90b5296a87d09341e3510608b191c9bc3a511f8e436"),
                new uint256("f4570fd8c54fded84b696ba3eb986a5421b0a41109dea6e10ba96aec70f78f00")
            };
            bool mutated;
            uint256 root = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);

            Assert.Equal("cd00f5d5aada62c8e49a9f01378998cbd016d04b725d0d8497877e5f75ffc722", root.ToString());
            Assert.False(mutated);
        }

        [Fact]
        public void MerkleRootComputationMutated()
        {
            var leaves = new List<uint256>()
            {
                new uint256("281f5acb40a65640bc48b90b5296a87d09341e3510608b191c9bc3a511f8e436"),
                new uint256("d0249653efaaa999f0278bb390c0f4ec3e5465a10f35264ebcfbb6dd6a677abd"),
                new uint256("7897c117fddbf98ea9749cc868a9d1e663b198dd3ac0ae5837734007f0060b20"),
                new uint256("ced314892a97f342a136269e2842fd0dbd1cab1fa84557bc48420f7cf96f0bc7"),

                new uint256("5b98af3d7554916483bca1a52f16570a93f07c95d6aeb8d08b0794c86cf58128"),
                new uint256("5b98af3d7554916483bca1a52f16570a93f07c95d6aeb8d08b0794c86cf58128"),
                new uint256("5b98af3d7554916483bca1a52f16570a93f07c95d6aeb8d08b0794c86cf58128"),

                new uint256("91be5a63b4b70c8329f20960e49a8bd3adeb77fcbadf78014ac54efaf1647a31"),
                new uint256("ac2dcf5c46d9a801389b6c0630b435ce5c2fa850cfac5456f16937dd8ae697d3")
            };
            bool mutated;
            uint256 root = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out mutated);

            Assert.Equal("95aa5bba66381c3817df338895349acd3fc3e8ce226e04a5e2acbb53db18b9c0", root.ToString());
            Assert.True(mutated);
        }
    }
}
