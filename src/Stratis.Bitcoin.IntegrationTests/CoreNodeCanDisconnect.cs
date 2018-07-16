using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.Builders;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests
{

    public class CoreNodeCanDisconnect : BddSpecification
    {
        private SharedSteps sharedSteps;
        private NodeGroupBuilder nodeGroupBuilder;
        private IDictionary<string, CoreNode> nodes;

        private const string AccountZero = "account 0";
        private const string WalletZero = "wallet 0";
        private const string WalletPassword = "123456";
        private const string JingTheFastMiner = "Jing";
        private const string Bob = "Bob";

        protected override void BeforeTest()
        {
            
        }

        protected override void AfterTest()
        {
            //this.nodeGroupBuilder.Dispose();
        }

        [Fact]
        public void can_connect_and_disconnect_and_mine()
        {
            this.sharedSteps = new SharedSteps();
            this.nodeGroupBuilder = new NodeGroupBuilder(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));

            this.nodes = this.nodeGroupBuilder
                .StratisPowNode(JingTheFastMiner).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .StratisPowNode(Bob).Start().NotInIBD().WithWallet(WalletZero, WalletPassword)
                .WithConnections()
                .Connect(JingTheFastMiner, Bob)
                .AndNoMoreConnections()
                .Build();

            this.sharedSteps.MineBlocks(1, this.nodes[JingTheFastMiner], AccountZero, WalletZero, WalletPassword);
            this.sharedSteps.WaitForNodesToSync(this.nodes[JingTheFastMiner], this.nodes[Bob]);

            this.nodes[JingTheFastMiner].FullNode.Chain.Tip.Height.Should().Be(1);
            this.nodes[Bob].FullNode.Chain.Tip.Height.Should().Be(1);

            this.nodes[JingTheFastMiner].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[Bob].Endpoint);
            this.nodes[Bob].FullNode.ConnectionManager.RemoveNodeAddress(this.nodes[JingTheFastMiner].Endpoint);

            this.sharedSteps.MineBlocks(5, this.nodes[JingTheFastMiner], AccountZero, WalletZero, WalletPassword);

            this.nodes[JingTheFastMiner].FullNode.Chain.Tip.Height.Should().Be(5);
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.nodes[Bob]));
            this.nodes[Bob].FullNode.Chain.Tip.Height.Should().Be(1);
        }

        public CoreNodeCanDisconnect(ITestOutputHelper output) : base(output)
        {
        }
    }
}
