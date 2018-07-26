using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class MinerSettingsTest : LogsTestBase	
    {	
        [Fact]	
        public void Load_GivenNodeSettings_LoadsSettingsFromNodeSettings()
        {	
            var nodeSettings = new NodeSettings(args: new string[] {
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
            var nodeSettings = new NodeSettings(args: new string[] {
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
            var nodeSettings = new NodeSettings(args: new string[] {
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
            var nodeSettings = new NodeSettings(args: new string[] {
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
            var nodeSettings = new NodeSettings(args: new string[] {
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
            var nodeSettings = new NodeSettings(args: new string[] {
                "-mine=true",
                "-blockmaxsize=5000000",
                "-blockmaxweight=5000000"
            });

            var minersettings = new MinerSettings(nodeSettings);
            
            // Values assigned maximum
            Assert.Equal(nodeSettings.Network.Consensus.Options.MaxBlockSerializedSize, minersettings.BlockDefinitionOptions.BlockMaxSize);
            Assert.Equal(nodeSettings.Network.Consensus.Options.MaxBlockWeight, minersettings.BlockDefinitionOptions.BlockMaxWeight);
        }
    }	
} 