using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    /// <summary>
    /// Tests of RPC controller action "getstakinginfo".
    /// </summary>
    public class GetStakingInfoActionTests
    {
        /// <summary>
        /// Tests that the RPC controller of a staking node correctly replies to "getstakinginfo" command.
        /// </summary>
        [Fact]
        public void GetStakingInfo_StakingEnabled()
        {
            using (var dir = TestDirectory.Create())
            {
                IFullNode fullNode = StratisBitcoinPosRunner.BuildStakingNode(dir.FolderName);
                var fullNodeRunTask = fullNode.RunAsync();

                INodeLifetime nodeLifetime = fullNode.NodeService<INodeLifetime>();
                nodeLifetime.ApplicationStarted.WaitHandle.WaitOne();
                MiningRPCController controller = fullNode.Services.ServiceProvider.GetService<MiningRPCController>();

                Assert.NotNull(fullNode.NodeService<IPosMinting>(true));

                GetStakingInfoModel info = controller.GetStakingInfo();

                Assert.NotNull(info);
                Assert.True(info.Enabled);
                Assert.False(info.Staking);

                nodeLifetime.StopApplication();
                nodeLifetime.ApplicationStopped.WaitHandle.WaitOne();
                fullNode.Dispose();

                Assert.False(fullNodeRunTask.IsFaulted);
            }
        }

        /// <summary>
        /// Tests that the RPC controller of a staking node correctly replies to "startstaking" command.
        /// </summary>
        [Fact]
        public void GetStakingInfo_StartStaking()
        {
            using (var dir = TestDirectory.Create())
            {
                IFullNode fullNode = StratisBitcoinPosRunner.BuildStakingNode(dir.FolderName, false);
                var node = fullNode as FullNode;

                var fullNodeRunTask = fullNode.RunAsync();

                INodeLifetime nodeLifetime = fullNode.NodeService<INodeLifetime>();
                nodeLifetime.ApplicationStarted.WaitHandle.WaitOne();
                MiningRPCController controller = fullNode.Services.ServiceProvider.GetService<MiningRPCController>();

                WalletManager walletManager = node.NodeService<IWalletManager>() as WalletManager;

                var password = "test";

                // create the wallet
                walletManager.CreateWallet(password, "test");

                Assert.NotNull(fullNode.NodeService<IPosMinting>(true));

                GetStakingInfoModel info = controller.GetStakingInfo();

                Assert.NotNull(info);
                Assert.False(info.Enabled);
                Assert.False(info.Staking);

                controller.StartStaking("test", "test");

                info = controller.GetStakingInfo();

                Assert.NotNull(info);
                Assert.True(info.Enabled);
                Assert.False(info.Staking);

                nodeLifetime.StopApplication();
                nodeLifetime.ApplicationStopped.WaitHandle.WaitOne();
                fullNode.Dispose();

                Assert.False(fullNodeRunTask.IsFaulted);
            }
        }
    }
}
