﻿using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
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

        /// <summary>
        /// Tests that the RPC controller of a staking node correctly replies to "startstaking" command.
        /// </summary>
        [Fact]
        public void GetStakingInfo_StartStaking()
        {
            string dir = AssureEmptyDir("TestData/GetStakingInfoActionTests/GetStakingInfo_StartStaking");
            IFullNode fullNode = this.BuildStakingNode(dir, false);
            var node = fullNode as FullNode;

            Task.Run(() =>
            {
                fullNode.Run();
            });

            INodeLifetime nodeLifetime = fullNode.NodeService<INodeLifetime>();
            nodeLifetime.ApplicationStarted.WaitHandle.WaitOne();
            MiningRPCController controller = fullNode.Services.ServiceProvider.GetService<MiningRPCController>();

            WalletManager walletManager = node.NodeService<IWalletManager>() as WalletManager;

            var password = "test";

            // create the wallet
            var mnemonic = walletManager.CreateWallet(password, "test");


            Assert.NotNull(fullNode.NodeService<PosMinting>(true));

            GetStakingInfoModel info = controller.GetStakingInfo();

            Assert.NotNull(info);
            Assert.Equal(false, info.Enabled);
            Assert.Equal(false, info.Staking);

            controller.StartStaking("test", "test");

            info = controller.GetStakingInfo();

            Assert.NotNull(info);
            Assert.Equal(true, info.Enabled);
            Assert.Equal(false, info.Staking);

            nodeLifetime.StopApplication();
            nodeLifetime.ApplicationStopped.WaitHandle.WaitOne();
            fullNode.Dispose();
        }
    }
}
