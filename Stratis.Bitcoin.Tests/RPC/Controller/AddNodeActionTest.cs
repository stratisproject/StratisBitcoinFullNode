using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.RPC.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{
    public class AddNodeActionTest : BaseRPCControllerTest
    {
        [TestMethod]
        public void CanCall()
        {
            string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/AddNodeActionTest/CanCall");
            IFullNode fullNode = this.BuildServicedNode(dir);
            ConnectionManagerController controller = fullNode.Services.ServiceProvider.GetService<ConnectionManagerController>();

            Assert.ThrowsException<System.Net.Sockets.SocketException>(() => { controller.AddNode("0.0.0.0", "onetry"); });
            Assert.ThrowsException<ArgumentException>(() => { controller.AddNode("0.0.0.0", "notarealcommand"); });
            Assert.ThrowsException<FormatException>(() => { controller.AddNode("a.b.c.d", "onetry"); });
            Assert.IsTrue(controller.AddNode("0.0.0.0", "remove"));
        }
    }
}
