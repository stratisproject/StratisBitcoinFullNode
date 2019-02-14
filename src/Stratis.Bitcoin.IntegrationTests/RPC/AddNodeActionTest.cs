using System;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [Fact]
        public void CanCall_AddNode()
        {
            string testDirectory = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(testDirectory);
            fullNode.Start();

            var controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            Assert.True(controller.AddNodeRPC("0.0.0.0", "add"));
            Assert.Throws<ArgumentException>(() => { controller.AddNodeRPC("0.0.0.0", "notarealcommand"); });
            Assert.ThrowsAny<SocketException>(() => { controller.AddNodeRPC("a.b.c.d", "onetry"); });
            Assert.True(controller.AddNodeRPC("0.0.0.0", "remove"));
        }

        [Fact]
        public void CanCall_AddNode_AddsNodeToCollection()
        {
            string testDirectory = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(testDirectory);

            var controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            var connectionManager = fullNode.NodeService<IConnectionManager>();
            controller.AddNodeRPC("0.0.0.0", "add");
            Assert.Single(connectionManager.ConnectionSettings.AddNode);
        }
    }
}