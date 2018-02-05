using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Miner.Tests
{
    public class MinerSettingsTest : LogsTestBase
    {
        [Fact]
        public void Load_GivenNodeSettings_LoadsSettingsFromNodeSettings()
        {
            bool callbackCalled = false;
            Action<MinerSettings> callback = (MinerSettings settings) =>
            {
                callbackCalled = true;
            };

            var minersettings = new MinerSettings(callback);

            var nodeSettings = new NodeSettings(args:new string[] {
                "-mine=true",
                "-stake=true",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            minersettings.Load(nodeSettings);

            Assert.True(minersettings.Mine);
            Assert.True(minersettings.Stake);
            Assert.Equal("mytestwallet", minersettings.WalletName);
            Assert.Equal("test", minersettings.WalletPassword);
            Assert.Equal("TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms", minersettings.MineAddress);
            Assert.True(callbackCalled);
        }

        [Fact]
        public void Load_MiningDisabled_DoesNotLoadMineAddress()
        {
            bool callbackCalled = false;
            Action<MinerSettings> callback = (MinerSettings settings) =>
            {
                callbackCalled = true;
            };

            var minersettings = new MinerSettings(callback);

            var nodeSettings = new NodeSettings(args:new string[] {
                "-mine=false",
                "-stake=true",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            minersettings.Load(nodeSettings);

            Assert.False(minersettings.Mine);
            Assert.True(minersettings.Stake);
            Assert.Equal("mytestwallet", minersettings.WalletName);
            Assert.Equal("test", minersettings.WalletPassword);
            Assert.Null(minersettings.MineAddress);
            Assert.True(callbackCalled);
        }

        [Fact]
        public void Load_StakingDisabled_DoesNotLoadWalletDetails()
        {
            bool callbackCalled = false;
            Action<MinerSettings> callback = (MinerSettings settings) =>
            {
                callbackCalled = true;
            };

            var minersettings = new MinerSettings(callback);

            var nodeSettings = new NodeSettings(args:new string[] {
                "-mine=true",
                "-stake=false",
                "-walletname=mytestwallet",
                "-walletpassword=test",
                "-mineaddress=TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms"
            });

            minersettings.Load(nodeSettings);

            Assert.True(minersettings.Mine);
            Assert.False(minersettings.Stake);
            Assert.Null(minersettings.WalletName);
            Assert.Null(minersettings.WalletPassword);
            Assert.Equal("TFE7R2FSAgAeJxt1fgW2YVCh9Zc448f3ms", minersettings.MineAddress);
            Assert.True(callbackCalled);
        }
    }
}