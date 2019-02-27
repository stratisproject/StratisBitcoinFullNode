using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using NSubstitute;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.Tests.Utils;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class LeaderProviderTests
    {
        private readonly IFederationGatewaySettings federationGatewaySettings;

        private readonly LeaderProvider leaderProvider;

        private List<string> leaderPubkeys;

        public LeaderProviderTests()
        {
            this.federationGatewaySettings = Substitute.For<IFederationGatewaySettings>();
            this.federationGatewaySettings.FederationPublicKeys.Returns(this.BuildLeaderPubKeys());
            this.leaderProvider = new LeaderProvider(this.federationGatewaySettings);
        }

        [Fact(Skip = TestingValues.SkipTests)]
        public void UpdatingBlockHeightCarriesOutLeaderPubKeysRoundRobin()
        {
            var output = new PubKey[10];

            for (int blockHeight = 0; blockHeight < 10; blockHeight++)
            {
                this.leaderProvider.Update(new BlockTipModel(new uint256(), blockHeight, 10));
                output[blockHeight] = this.leaderProvider.CurrentLeaderKey;
            }

            this.leaderPubkeys = this.leaderPubkeys.OrderBy(k => k).ToList();

            for (int i = 0; i < 5; i++)
            {
                output[i].ToString().Should().Contain(this.leaderPubkeys[i]);
                output[i + 5].ToString().Should().Contain(this.leaderPubkeys[i]);
            }
        }

        private PubKey[] BuildLeaderPubKeys()
        {
            // Unordered list.
            this.leaderPubkeys = new List<string>()
            {
                "026ebcbf6bfe7ce1d957adbef8ab2b66c788656f35896a170257d6838bda70b95c",
                "03c99f997ed71c7f92cf532175cea933f2f11bf08f1521d25eb3cc9b8729af8bf4",
                "02e9d3cd0c2fa501957149ff9d21150f3901e6ece0e3fe3007f2372720c84e3ee1",
                "02a97b7d0fad7ea10f456311dcd496ae9293952d4c5f2ebdfc32624195fde14687",
                "034b191e3b3107b71d1373e840c5bf23098b55a355ca959b968993f5dec699fc38"
            };

            return this.leaderPubkeys.Select(s => new PubKey(s)).OrderBy(z => z.ToString()).ToArray();
        }
    }
}
