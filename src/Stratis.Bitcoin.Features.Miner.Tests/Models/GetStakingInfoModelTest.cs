using Stratis.Bitcoin.Features.Miner.Models;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests.Models
{
    public class GetStakingInfoModelTest
    {
        [Fact]
        public void Clone_CreatesCloneOfModel()
        {
            var model = new GetStakingInfoModel()
            {
                CurrentBlockSize = 15,
                CurrentBlockTx = 12,
                Difficulty = 1.2,
                Enabled = true,
                Errors = "Error",
                ExpectedTime = 10,
                NetStakeWeight = 12000,
                PooledTx = 56,
                SearchInterval = 13,
                Staking = true,
                Weight = 34
            };

            var clone = (GetStakingInfoModel)model.Clone();

            Assert.Equal(model.CurrentBlockSize, clone.CurrentBlockSize);
            Assert.Equal(model.CurrentBlockTx, clone.CurrentBlockTx);
            Assert.Equal(model.Difficulty, clone.Difficulty);
            Assert.Equal(model.Enabled, clone.Enabled);
            Assert.Equal(model.Errors, clone.Errors);
            Assert.Equal(model.ExpectedTime, clone.ExpectedTime);
            Assert.Equal(model.NetStakeWeight, clone.NetStakeWeight);
            Assert.Equal(model.PooledTx, clone.PooledTx);
            Assert.Equal(model.SearchInterval, clone.SearchInterval);
            Assert.Equal(model.Staking, clone.Staking);
            Assert.Equal(model.Weight, clone.Weight);
        }
    }
}
