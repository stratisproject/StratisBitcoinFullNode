using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests
{
    public class SlotsManagerTests
    {
        private readonly SlotsManager slotsManager;
        private readonly PoANetwork network;

        public SlotsManagerTests()
        {
            this.network = new TestPoANetwork();

            this.slotsManager = new SlotsManager(this.network, new LoggerFactory());
        }

        [Fact]
        public void IsValidTimestamp()
        {
            uint targetSpacing = this.network.TargetSpacingSeconds;

            Assert.True(this.slotsManager.IsValidTimestamp(targetSpacing));
            Assert.True(this.slotsManager.IsValidTimestamp(targetSpacing * 100));
            Assert.False(this.slotsManager.IsValidTimestamp(targetSpacing * 10 + 1));
            Assert.False(this.slotsManager.IsValidTimestamp(targetSpacing + 2));
        }

        [Fact]
        public void ValidSlotAssigned()
        {
            List<PubKey> fedKeys = this.network.FederationPublicKeys;
            uint roundStart = this.network.TargetSpacingSeconds * (uint)fedKeys.Count * 5;

            Assert.Equal(fedKeys[0], this.slotsManager.GetPubKeyForTimestamp(roundStart));
            Assert.Equal(fedKeys[1], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 1));
            Assert.Equal(fedKeys[2], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 2));
            Assert.Equal(fedKeys[3], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 3));
            Assert.Equal(fedKeys[4], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 4));
            Assert.Equal(fedKeys[5], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 5));
            Assert.Equal(fedKeys[0], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 6));
            Assert.Equal(fedKeys[1], this.slotsManager.GetPubKeyForTimestamp(roundStart + this.network.TargetSpacingSeconds * 7));
        }

        private class TestPoANetwork : PoANetwork
        {
            public TestPoANetwork()
            {
                this.TargetSpacingSeconds = 60;

                this.FederationPublicKeys = new List<PubKey>()
                {
                    new PubKey("02d485fc5ae101c2780ff5e1f0cb92dd907053266f7cf3388eb22c5a4bd266ca2e"),
                    new PubKey("026ed3f57de73956219b85ef1e91b3b93719e2645f6e804da4b3d1556b44a477ef"),
                    new PubKey("03895a5ba998896e688b7d46dd424809b0362d61914e1432e265d9539fe0c3cac0"),
                    new PubKey("020fc3b6ac4128482268d96f3bd911d0d0bf8677b808eaacd39ecdcec3af66db34"),
                    new PubKey("038d196fc2e60d6dfc533c6a905ba1f9092309762d8ebde4407d209e37a820e462"),
                    new PubKey("0358711f76435a508d98a9dee2a7e160fed5b214d97e65ea442f8f1265d09e6b55")
                };
            }
        }
    }
}
