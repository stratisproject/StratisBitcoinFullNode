using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.RPC.Controllers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.RPC.Controller
{    
    public class MempoolActionTests : BaseRPCControllerTest
    {
        [TestMethod]
        public async Task CanCall()
        {
			Logs.Configure(new LoggerFactory());

			string dir = AssureEmptyDir("Stratis.Bitcoin.Tests/TestData/GetRawMempoolActionTest/CanCall");
            IFullNode fullNode = this.BuildServicedNode(dir);
            MempoolController controller = fullNode.Services.ServiceProvider.GetService<MempoolController>();

            List<uint256> result = await controller.GetRawMempool();

            Assert.IsNotNull(result);
        }
    }
}
