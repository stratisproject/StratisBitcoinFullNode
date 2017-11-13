using System;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Connection;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests.Controller
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [Fact]
        public void CanCall_AddNode()
        {
            string dir = AssureEmptyDir("TestData/AddNodeActionTest/CanCall_AddNode");
            IFullNode fullNode = this.BuildServicedNode(dir);
            ConnectionManagerController controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            Assert.Throws<System.Net.Sockets.SocketException>(() => { controller.AddNode("0.0.0.0", "onetry"); });
            Assert.Throws<ArgumentException>(() => { controller.AddNode("0.0.0.0", "notarealcommand"); });
            Assert.Throws<FormatException>(() => { controller.AddNode("a.b.c.d", "onetry"); });
            Assert.True(controller.AddNode("0.0.0.0", "remove"));
        }

    }
}
