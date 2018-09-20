using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class FinalizedBlockInfoRepositoryTest : TestBase
    {
        public FinalizedBlockInfoRepositoryTest() : base(KnownNetworks.StratisRegTest)
        {
        }

        [Fact]
        public async Task FinalizedHeightSavedOnDiskAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new FinalizedBlockInfoRepository(dir, new LoggerFactory()))
            {
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 777);
            }

            using (var repo = new FinalizedBlockInfoRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockInfoAsync(new StratisMain());
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }

        [Fact]
        public async Task FinalizedHeightCantBeDecreasedAsync()
        {
            string dir = CreateTestDir(this);

            using (var repo = new FinalizedBlockInfoRepository(dir, new LoggerFactory()))
            {
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 777);
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 555);

                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }

            using (var repo = new FinalizedBlockInfoRepository(dir, new LoggerFactory()))
            {
                await repo.LoadFinalizedBlockInfoAsync(new StratisMain());
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }
    }
}
