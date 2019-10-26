using System;
using FluentAssertions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class MinerSettingsTest : LogsTestBase
    {
        public MinerSettingsTest() : base(KnownNetworks.StratisTest)
        {
        }

        [Fact]
        public void Load_GivenNodeSettings_LoadsSettingsFromNodeSettings()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-mine=true",
                "-stake=true",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            var minersettings = new MinerSettings(nodeSettings);

            Assert.True(minersettings.Mine);
            Assert.True(minersettings.Stake);
            Assert.Equal("mytestwallet", minersettings.WalletName);
            Assert.Equal("test", minersettings.WalletPassword);
            Assert.Equal("TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms", minersettings.MineAddress);
        }

        [Fact]
        public void Load_MiningDisabled_DoesNotLoadMineAddress()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-mine=false",
                "-stake=true",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            var minersettings = new MinerSettings(nodeSettings);


            Assert.False(minersettings.Mine);
            Assert.True(minersettings.Stake);
            Assert.Equal("mytestwallet", minersettings.WalletName);
            Assert.Equal("test", minersettings.WalletPassword);
            Assert.Null(minersettings.MineAddress);
        }

        [Fact]	
        public void Load_StakingDisabled_DoesNotLoadWalletDetails()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-mine=true",
                "-stake=false",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            var minersettings = new MinerSettings(nodeSettings);

            Assert.True(minersettings.Mine);
            Assert.False(minersettings.Stake);
            Assert.Null(minersettings.WalletName);
            Assert.Null(minersettings.WalletPassword);
            Assert.Equal("TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms", minersettings.MineAddress);
        }

        [Fact]
        public void Load_MiningEnabled_BlockSize_BlockWeight_Set()
        {
            // Set values within consensus rules
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-mine=true",
                "-blockmaxsize=150000",
                "-blockmaxweight=300000"
            });

            var minersettings = new MinerSettings(nodeSettings);

            // Values assigned as configured
            Assert.Equal((uint)150000, minersettings.BlockDefinitionOptions.BlockMaxSize);
            Assert.Equal((uint)300000, minersettings.BlockDefinitionOptions.BlockMaxWeight);
        }

        [Fact]
        public void Load_MiningEnabled_BlockSize_BlockWeight_Set_BelowMinimum()
        {
            // Set values below consensus rules
            var nodeSettings = new NodeSettings(KnownNetworks.TestNet, args: new string[] {
                "-mine=true",
                "-blockmaxsize=10",
                "-blockmaxweight=30"
            });

            var minersettings = new MinerSettings(nodeSettings);

            // Values assigned minimum
            Assert.Equal((uint)1000, minersettings.BlockDefinitionOptions.BlockMaxSize);
            Assert.Equal((uint)4000, minersettings.BlockDefinitionOptions.BlockMaxWeight);
        }

        [Fact]
        public void Load_MiningEnabled_BlockSize_BlockWeight_Set_AboveMaximum()
        {
            // Set values above consensus rules
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-mine=true",
                "-blockmaxsize=5000000",
                "-blockmaxweight=5000000"
            });

            var minersettings = new MinerSettings(nodeSettings);
            
            // Values assigned maximum
            Assert.Equal(nodeSettings.Network.Consensus.Options.MaxBlockSerializedSize, minersettings.BlockDefinitionOptions.BlockMaxSize);
            Assert.Equal(nodeSettings.Network.Consensus.Options.MaxBlockWeight, minersettings.BlockDefinitionOptions.BlockMaxWeight);
        }

        [Fact]
        public void Load_EnableCoinStakeSplitting_MinimumStakingCoinValue_And_MinimumSplitCoinValue()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-enablecoinstakesplitting=false",
                "-minimumstakingcoinvalue=50000",
                "-minimumsplitcoinvalue=50000000"
            });

            var minerSettings = new MinerSettings(nodeSettings);

            minerSettings.EnableCoinStakeSplitting.Should().BeFalse();
            minerSettings.MinimumStakingCoinValue.Should().Be(50000);
            minerSettings.MinimumSplitCoinValue.Should().Be(50000000);
        }

        [Fact]
        public void Load_MinimumStakingCoinValue_Should_Be_Strictly_Above_Zero()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-minimumstakingcoinvalue=0",
            });

            var minerSettings = new MinerSettings(nodeSettings);

            minerSettings.MinimumStakingCoinValue.Should().Be(1);
        }

        [Fact]
        public void Defaults_EnableCoinStakeSplitting_MinimumStakingCoinValue_And_MinimumSplitCoinValue()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-stake=1",
            });

            var minerSettings = new MinerSettings(nodeSettings);

            minerSettings.EnableCoinStakeSplitting.Should().BeTrue();
            minerSettings.MinimumStakingCoinValue.Should().Be(10000000);
            minerSettings.MinimumSplitCoinValue.Should().Be(10000000000);
        }

        [Fact]
        public void Throws_On_MinimumStakingCoinValue_Or_MinimumSplitCoinValue_Invalid()
        {
            var nodeSettings = new NodeSettings(this.Network, args: new string[] {
               "-minimumstakingcoinvalue=-1",
            });

            new Action(() => new MinerSettings(nodeSettings)).Should().Throw<Exception>();

            nodeSettings = new NodeSettings(this.Network, args: new string[] {
                "-minimumsplitcoinvalue=-1"
            });

            new Action(() => new MinerSettings(nodeSettings)).Should().Throw<Exception>();
        }
    }
} 