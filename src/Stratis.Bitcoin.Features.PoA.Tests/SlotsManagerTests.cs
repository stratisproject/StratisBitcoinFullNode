using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class SlotsManagerTests
    {
        private ISlotsManager slotsManager;
        private TestPoANetwork network;
        private readonly PoAConsensusOptions consensusOptions;
        private readonly IFederationManager federationManager;
        private Mock<IConsensusManager> consensusManagerMock;

        public SlotsManagerTests()
        {
            this.network = new TestPoANetwork();
            this.consensusOptions = this.network.ConsensusOptions;

            this.federationManager = PoATestsBase.CreateFederationManager(this);
            this.consensusManagerMock = new Mock<IConsensusManager>();
            this.slotsManager = new SlotsManager(this.network, this.federationManager, this.consensusManagerMock.Object, new LoggerFactory());
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
            List<IFederationMember> federationMembers = this.federationManager.GetFederationMembers();
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)federationMembers.Count * 5;

            int currentFedIndex = -1;

            for (int i = 0; i < 20; i++)
            {
                currentFedIndex++;
                if (currentFedIndex > federationMembers.Count - 1)
                    currentFedIndex = 0;

                Assert.Equal(federationMembers[currentFedIndex].PubKey, this.slotsManager.GetFederationMemberForTimestamp(roundStart + this.consensusOptions.TargetSpacingSeconds * (uint)i).PubKey);
            }
        }

        [Fact]
        public void GetMiningTimestamp()
        {
            var tool = new KeyTool(new DataFolder(string.Empty));
            Key key = tool.GeneratePrivateKey();
            this.network = new TestPoANetwork(new List<PubKey>() { tool.GeneratePrivateKey().PubKey, key.PubKey, tool.GeneratePrivateKey().PubKey });

            IFederationManager fedManager = PoATestsBase.CreateFederationManager(this, this.network, new ExtendedLoggerFactory(), new Signals.Signals(new LoggerFactory(), null));
            this.consensusManagerMock.Setup(x => x.Tip).Returns(new ChainedHeader(new BlockHeader(), 0, 0));
            this.slotsManager = new SlotsManager(this.network, fedManager, this.consensusManagerMock.Object, new LoggerFactory());

            List<IFederationMember> federationMembers = fedManager.GetFederationMembers();
            uint roundStart = this.consensusOptions.TargetSpacingSeconds * (uint)federationMembers.Count * 5;

            fedManager.SetPrivatePropertyValue(typeof(FederationManagerBase), nameof(IFederationManager.CurrentFederationKey), key);
            fedManager.SetPrivatePropertyValue(typeof(FederationManagerBase), nameof(this.federationManager.IsFederationMember), true);

            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart + 4));

            roundStart += this.consensusOptions.TargetSpacingSeconds * (uint)federationMembers.Count;
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - 5));
            Assert.Equal(roundStart + this.consensusOptions.TargetSpacingSeconds, this.slotsManager.GetMiningTimestamp(roundStart - this.consensusOptions.TargetSpacingSeconds + 1));

            Assert.True(this.slotsManager.IsValidTimestamp(this.slotsManager.GetMiningTimestamp(roundStart - 5)));

            uint actualNextTurnTimestamp = roundStart + this.consensusOptions.TargetSpacingSeconds;

            // If we ask for our next timestamp, and we are just after our opportunity to mine...

            // If we are closer to the next turn than our own, get the next turn's' timestamp
            Assert.Equal(actualNextTurnTimestamp + this.consensusOptions.TargetSpacingSeconds * (uint)federationMembers.Count,
                this.slotsManager.GetMiningTimestamp(actualNextTurnTimestamp + (this.consensusOptions.TargetSpacingSeconds / 2) + 1));

            // If we have not already mined, get this turn's time
            Assert.Equal(actualNextTurnTimestamp, this.slotsManager.GetMiningTimestamp(actualNextTurnTimestamp + 1));

            // If we have already mined, get the next turn's time.
            this.consensusManagerMock.Setup(x => x.Tip).Returns(new ChainedHeader(new BlockHeader
            {
                Time = actualNextTurnTimestamp
            }, 0, 0));
            this.slotsManager = new SlotsManager(this.network, fedManager, this.consensusManagerMock.Object, new LoggerFactory());
            Assert.Equal(actualNextTurnTimestamp + this.consensusOptions.TargetSpacingSeconds * (uint)federationMembers.Count, this.slotsManager.GetMiningTimestamp(actualNextTurnTimestamp + 1));

        }
    }
}
