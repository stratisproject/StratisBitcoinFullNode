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
    }	
} 