using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC.Models;
using Stratis.Bitcoin.Utilities;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    /// <summary>
    /// Tests of RPC controller action "getstakinginfo".
    /// </summary>
    public class GetStakingInfoActionTests : BaseRPCControllerTest
    {
        /// <summary>
        /// Tests that the RPC controller of a staking node correctly replies to "getstakinginfo" command.
        /// </summary>
        [Fact]
        public void GetStakingInfo_StakingEnabled()
        {
            string dir = AssureEmptyDir("TestData/GetStakingInfoActionTests/GetStakingInfo_StakingEnabled");
            IFullNode fullNode = this.BuildStakingNode(dir);
            Task.Run(() =>
            {
                fullNode.Run();
            });

            INodeLifetime nodeLifetime = fullNode.NodeService<INodeLifetime>();
            nodeLifetime.ApplicationStarted.WaitHandle.WaitOne();
            MiningRPCController controller = fullNode.Services.ServiceProvider.GetService<MiningRPCController>();

            Assert.NotNull(fullNode.NodeService<PosMinting>(true));

            GetStakingInfoModel info = controller.GetStakingInfo();

            Assert.NotNull(info);
            Assert.Equal(true, info.Enabled);
            Assert.Equal(false, info.Staking);

            nodeLifetime.StopApplication();
            nodeLifetime.ApplicationStopped.WaitHandle.WaitOne();
            fullNode.Dispose();
        }
    }
}
