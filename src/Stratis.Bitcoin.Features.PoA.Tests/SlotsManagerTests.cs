using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.PoA.Tests.Rules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class SlotsManagerTests
    {
        private SlotsManager slotsManager;
        private TestPoANetwork network;
        private PoAConsensusOptions consensusOptions;
        private FederationManager federationManager;

        public SlotsManagerTests()
        {
            this.network = new TestPoANetwork();
            this.consensusOptions = this.network.ConsensusOptions;

            this.federationManager = PoATestsBase.CreateFederationManager(this);
            this.slotsManager = new SlotsManager(this.network, this.federationManager, new LoggerFactory());
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
            List<PubKey> fedKeys = this.federationManager.GetFederationMembers();
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)fedKeys.Count * 5;


            int currentFedIndex = -1;

            for (int i = 0; i < 20; i++)
            {
                currentFedIndex++;
                if (currentFedIndex > fedKeys.Count - 1)
                    currentFedIndex = 0;

                Assert.Equal(fedKeys[currentFedIndex], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * (uint)i));
            }
        }

        [Fact]
        public void GetMiningTimestamp()
        {
            var tool = new KeyTool(new DataFolder(string.Empty));
            Key key = tool.GeneratePrivateKey();
            this.network = new TestPoANetwork(new List<PubKey>() { tool.GeneratePrivateKey().PubKey, key.PubKey, tool.GeneratePrivateKey().PubKey});

            FederationManager fedManager = PoATestsBase.CreateFederationManager(this, this.network, new ExtendedLoggerFactory());
            this.slotsManager = new SlotsManager(this.network, fedManager, new LoggerFactory());

            List<PubKey> fedKeys = this.federationManager.GetFederationMembers();
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)fedKeys.Count * 5;

            fedManager.SetPrivatePropertyValue(nameof(FederationManager.FederationMemberKey), key);
            fedManager.SetPrivatePropertyValue(nameof(this.federationManager.IsFederationMember), true);

            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart + 4));

            roundStart += this.consensusOptions.TargetSpacingSeconds * (uint) fedKeys.Count;
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - 5));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - this.consensusOptions.TargetSpacingSeconds + 1));

            Assert.True(this.slotsManager.IsValidTimestamp(this.slotsManager.GetMiningTimestamp(roundStart - 5)));
        }
    }
}
