using System;
using System.Collections.Generic;
using System.Security;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Features.Miner.Controllers;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.RPC.Exceptions;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests.Controllers
{
    public class MiningRpcControllerTest : LogsTestBase, IClassFixture<MiningRPCControllerFixture>
    {
        private readonly MiningRpcController miningRpcController;
        private StakingRpcController stakingRpcController;
        private readonly Mock<IFullNode> fullNode;
        private readonly Mock<IPosMinting> posMinting;
        private readonly Mock<IWalletManager> walletManager;
        private readonly Mock<ITimeSyncBehaviorState> timeSyncBehaviorState;
        private readonly MiningRPCControllerFixture fixture;
        private readonly Mock<IPowMining> powMining;

        public MiningRpcControllerTest(MiningRPCControllerFixture fixture)
        {
            this.fixture = fixture;
            this.powMining = new Mock<IPowMining>();
            this.fullNode = new Mock<IFullNode>();
            this.posMinting = new Mock<IPosMinting>();
            this.walletManager = new Mock<IWalletManager>();
            this.timeSyncBehaviorState = new Mock<ITimeSyncBehaviorState>();
            this.fullNode.Setup(f => f.NodeService<IWalletManager>(false))
                .Returns(this.walletManager.Object);

            this.miningRpcController = new MiningRpcController(this.powMining.Object, this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object);
            this.stakingRpcController = new StakingRpcController(this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object, this.posMinting.Object);
        }


        [Fact]
        public void Generate_BlockCountLowerThanZero_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.miningRpcController.Generate(-1);
            });
        }

        [Fact]
        public void Generate_NoWalletLoaded_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.walletManager.Setup(w => w.GetWalletsNames())
                    .Returns(new List<string>());

                this.miningRpcController.Generate(10);
            });
        }

        [Fact]
        public void Generate_WalletWithoutAccount_ThrowsRPCServerException()
        {
            Assert.Throws<RPCServerException>(() =>
            {
                this.walletManager.Setup(w => w.GetWalletsNames())
                    .Returns(new List<string>() {
                        "myWallet"
                    });

                this.walletManager.Setup(w => w.GetAccounts("myWallet"))
                    .Returns(new List<HdAccount>());

                this.miningRpcController.Generate(10);
            });
        }

        [Fact]
        public void Generate_UnusedAddressCanBeFoundOnWallet_GeneratesBlocksUsingAddress_ReturnsBlockHeaderHashes()
        {
            this.walletManager.Setup(w => w.GetWalletsNames())
                   .Returns(new List<string>() {
                        "myWallet"
                   });
            this.walletManager.Setup(w => w.GetAccounts("myWallet"))
                .Returns(new List<HdAccount>() {
                    WalletTestsHelpers.CreateAccount("test")
                });
            HdAddress address = WalletTestsHelpers.CreateAddress(false);
            this.walletManager.Setup(w => w.GetUnusedAddress(It.IsAny<WalletAccountReference>()))
                .Returns(address);

            this.powMining.Setup(p => p.GenerateBlocks(It.Is<ReserveScript>(r => r.ReserveFullNodeScript == address.Pubkey), 1, int.MaxValue))
                .Returns(new List<NBitcoin.uint256>() {
                    new NBitcoin.uint256(1255632623)
                });

            List<uint256> result = this.miningRpcController.Generate(1);

            Assert.NotEmpty(result);

            Assert.Equal(new NBitcoin.uint256(1255632623), result[0]);
        }

        [Fact]
        public void StartStaking_WalletNotFound_ThrowsWalletException()
        {
            Assert.Throws<WalletException>(() =>
            {
                this.walletManager.Setup(w => w.GetWallet("myWallet"))
                .Throws(new WalletException("Wallet not found."));

                this.stakingRpcController.StartStaking("myWallet", "password");
            });  
        }
   
        [Fact]
        public void StartStaking_InvalidWalletPassword_ThrowsSecurityException()
        {
            Assert.Throws<SecurityException>(() =>
            {               
                this.walletManager.Setup(w => w.GetWallet("myWallet"))
                  .Returns(this.fixture.wallet);

                this.stakingRpcController.StartStaking("myWallet", "password");
            });
        }

        [Fact]
        public void StartStaking_InvalidTimeSyncState_ThrowsException()
        {
            this.walletManager.Setup(w => w.GetWallet("myWallet")).Returns(this.fixture.wallet);
            this.timeSyncBehaviorState.Setup(ts => ts.IsSystemTimeOutOfSync).Returns(true);

            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(true))
                .Returns(new MiningFeature(new ConnectionManagerSettings(NodeSettings.Default(this.Network)), this.Network, new MinerSettings(NodeSettings.Default(this.Network)), NodeSettings.Default(this.Network), this.LoggerFactory.Object, this.timeSyncBehaviorState.Object, this.powMining.Object, this.posMinting.Object));

            var exception = Assert.Throws<ConfigurationException>(() =>
            {
                bool result = this.stakingRpcController.StartStaking("myWallet", "password1");
            });

            Assert.Contains("Staking cannot start", exception.Message);
            this.posMinting.Verify(pm => pm.Stake(It.IsAny<WalletSecret>()), Times.Never);
        }

        [Fact]
        public void StartStaking_ValidWalletAndPassword_StartsStaking_ReturnsTrue()
        {            
            this.walletManager.Setup(w => w.GetWallet("myWallet"))
              .Returns(this.fixture.wallet);

            this.fullNode.Setup(f => f.NodeFeature<MiningFeature>(true))
                .Returns(new MiningFeature(new ConnectionManagerSettings(NodeSettings.Default(this.Network)), this.Network, new MinerSettings(NodeSettings.Default(this.Network)), NodeSettings.Default(this.Network), this.LoggerFactory.Object, this.timeSyncBehaviorState.Object, this.powMining.Object, this.posMinting.Object));

            bool result = this.stakingRpcController.StartStaking("myWallet", "password1");

            Assert.True(result);
            this.posMinting.Verify(p => p.Stake(It.Is<WalletSecret>(s => s.WalletName == "myWallet" && s.WalletPassword == "password1")), Times.Exactly(1));
        }

        [Fact]
        public void GetStakingInfo_WithoutPosMinting_ReturnsEmptyStakingInfoModel()
        {
            this.stakingRpcController = new StakingRpcController(this.fullNode.Object, this.LoggerFactory.Object, this.walletManager.Object, null);

            GetStakingInfoModel result = this.stakingRpcController.GetStakingInfo(true);

            Assert.Equal(JsonConvert.SerializeObject(new GetStakingInfoModel()), JsonConvert.SerializeObject(result));
        }

        [Fact]
        public void GetStakingInfo_WithPosMinting_ReturnsPosMintingStakingInfoModel()
        {
            this.posMinting.Setup(p => p.GetGetStakingInfoModel())
                .Returns(new GetStakingInfoModel()
                {
                    Enabled = true,
                    CurrentBlockSize = 150000
                }).Verifiable();

            GetStakingInfoModel result = this.stakingRpcController.GetStakingInfo(true);
            
            Assert.True(result.Enabled);
            Assert.Equal(150000, result.CurrentBlockSize);
            this.posMinting.Verify();
        }

        [Fact]
        public void GetStakingInfo_NotJsonFormat_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => {
                this.stakingRpcController.GetStakingInfo(false);
            });
        }
    }

    public class MiningRPCControllerFixture
    {
        public readonly Wallet.Wallet wallet;

        public MiningRPCControllerFixture()
        {
            this.wallet = WalletTestsHelpers.GenerateBlankWallet("myWallet", "password1");
        }
    }
}
