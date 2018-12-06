using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.Tests.Rules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class SlotsManagerTests
    {
        private SlotsManager slotsManager;
        private PoANetwork network;
        private PoAConsensusOptions consensusOptions;

        public SlotsManagerTests()
        {
            this.network = new TestPoANetwork();
            this.consensusOptions = this.network.ConsensusOptions;

            var fedManager = new FederationManager(NodeSettings.Default(this.network), this.network, new LoggerFactory());
            this.slotsManager = new SlotsManager(this.network, fedManager, new LoggerFactory());
        }

        [Fact]
        public void IsValidTimestamp()
        {
            uint targetSpacing = this.consensusOptions.TargetSpacingSeconds;

            Assert.True(this.slotsManager.IsValidTimestamp(targetSpacing));
            Assert.True(this.slotsManager.IsValidTimestamp(targetSpacing * 100));
            Assert.False(this.slotsManager.IsValidTimestamp(targetSpacing * 10 + 1));
            Assert.False(this.slotsManager.IsValidTimestamp(targetSpacing + 2));
        }

        [Fact]
        public void ValidSlotAssigned()
        {
            List<PubKey> fedKeys = this.network.ConsensusOptions.FederationPublicKeys;
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)fedKeys.Count * 5;

            Assert.Equal(fedKeys[0], this.slotsManager.GetPubKeyForTimestamp(roundStart));
            Assert.Equal(fedKeys[1], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 1));
            Assert.Equal(fedKeys[2], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 2));
            Assert.Equal(fedKeys[3], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 3));
            Assert.Equal(fedKeys[4], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 4));
            Assert.Equal(fedKeys[5], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 5));
            Assert.Equal(fedKeys[0], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 6));
            Assert.Equal(fedKeys[1], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * 7));
        }

        [Fact]
        public void GetMiningTimestamp()
        {
            var tool = new KeyTool(new DataFolder(string.Empty));
            Key key = tool.GeneratePrivateKey();
            this.network = new TestPoANetwork(new List<PubKey>() { tool.GeneratePrivateKey().PubKey, key.PubKey, tool.GeneratePrivateKey().PubKey});

            var fedManager = new FederationManager(NodeSettings.Default(this.network), this.network, new LoggerFactory());
            this.slotsManager = new SlotsManager(this.network, fedManager, new LoggerFactory());

            List<PubKey> fedKeys = this.consensusOptions.FederationPublicKeys;
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)fedKeys.Count * 5;

            fedManager.SetPrivatePropertyValue(nameof(FederationManager.IsFederationMember), true);
            fedManager.SetPrivatePropertyValue(nameof(FederationManager.FederationMemberKey), key);

            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart + 4));

            roundStart += this.consensusOptions.TargetSpacingSeconds * (uint) fedKeys.Count;
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - 5));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - this.consensusOptions.TargetSpacingSeconds + 1));

            Assert.True(this.slotsManager.IsValidTimestamp(this.slotsManager.GetMiningTimestamp(roundStart - 5)));
        }
    }
}
