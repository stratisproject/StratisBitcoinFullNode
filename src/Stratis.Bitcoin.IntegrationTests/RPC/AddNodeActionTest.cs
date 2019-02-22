using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.RPC
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [Fact]
        public async Task CanCall_AddNodeAsync()
        {
            string testDirectory = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(testDirectory);
            fullNode.Start();

            var controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            var addResult = await controller.AddNodeRPCAsync("0.0.0.0", "add");
            Assert.True(addResult.Success);

            var invalidCommandResult = await controller.AddNodeRPCAsync("0.0.0.0", "notarealcommand");
            Assert.Equal("An invalid command was specified, only 'add', 'remove' or 'onetry' is supported.", invalidCommandResult.ErrorMessage);
            Assert.False(invalidCommandResult.Success);

            Assert.ThrowsAny<SocketException>(() => { controller.AddNodeRPCAsync("a.b.c.d", "onetry").GetAwaiter().GetResult(); });

            var removeResult = await controller.AddNodeRPCAsync("0.0.0.0", "remove");
            Assert.True(removeResult.Success);
        }

        [Fact]
        public async Task CanCall_AddNode_AddsNodeToCollectionAsync()
        {
            string testDirectory = CreateTestDir(this);

            IFullNode fullNode = this.BuildServicedNode(testDirectory);

            var controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            var connectionManager = fullNode.NodeService<IConnectionManager>();
            await controller.AddNodeRPCAsync("0.0.0.0", "add");

            Assert.Single(connectionManager.ConnectionSettings.AddNode);
        }
    }
}