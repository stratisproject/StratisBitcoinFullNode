using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [Fact]
        public void CanCall_AddNode()
        {
            var initialBlockSignature = Block.BlockSignature;

            try
            {
                Block.BlockSignature = false;
                string testDirectory = CreateTestDir(this);


                IFullNode fullNode = this.BuildServicedNode(testDirectory);
                ConnectionManagerController controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

                Assert.ThrowsAny<System.Net.Sockets.SocketException>(() => { controller.AddNode("0.0.0.0", "onetry"); });
                Assert.Throws<ArgumentException>(() => { controller.AddNode("0.0.0.0", "notarealcommand"); });
                Assert.Throws<FormatException>(() => { controller.AddNode("a.b.c.d", "onetry"); });
                Assert.True(controller.AddNode("0.0.0.0", "remove"));
            }
            finally
            {
                Block.BlockSignature = initialBlockSignature;
            }
        }

        [Fact]
        public void CanCall_AddNode_AddsNodeToCollection()
        {
            var initialBlockSignature = Block.BlockSignature;

            try
            {
                Block.BlockSignature = false;
                string testDirectory = CreateTestDir(this);

                IFullNode fullNode = this.BuildServicedNode(testDirectory);

                ConnectionManagerController controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

                var connectionManager = fullNode.NodeService<IConnectionManager>();
                controller.AddNode("0.0.0.0", "add");
                Assert.Single(connectionManager.ConnectionSettings.AddNode);
            }
            finally
            {
                Block.BlockSignature = initialBlockSignature;
            }

        }
    }
}