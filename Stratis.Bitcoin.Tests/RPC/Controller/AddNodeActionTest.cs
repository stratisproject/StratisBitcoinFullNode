using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Xunit;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [Fact]
        public void CanCall()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/AddNodeActionTest/CanCall");
            IFullNode fullNode = this.BuildServicedNode(dir);
            ConnectionManagerController controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            Assert.Throws(typeof(System.Net.Sockets.SocketException), () => { controller.AddNode("0.0.0.0", "onetry"); });
            Assert.Throws(typeof(ArgumentException), () => { controller.AddNode("0.0.0.0", "notarealcommand"); });
            Assert.Throws(typeof(FormatException), () => { controller.AddNode("a.b.c.d", "onetry"); });
            Assert.True(controller.AddNode("0.0.0.0", "remove"));
        }

    }
}
