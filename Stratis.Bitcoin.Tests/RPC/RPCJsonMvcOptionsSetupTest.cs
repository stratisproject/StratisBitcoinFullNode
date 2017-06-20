using System.Buffers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Stratis.Bitcoin.RPC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.RPC
{
    [TestClass]
    public class RPCJsonMvcOptionsSetupTest
    {
        [TestMethod]
        public void ConfigureMvcReplacesJsonFormattedWithRPCJsonOutputFormatter()
        {
            var settings = new JsonSerializerSettings();
            var charpool = ArrayPool<char>.Create();
            var options = new MvcOptions();
            options.OutputFormatters.Clear();
            options.OutputFormatters.Add(new JsonOutputFormatter(settings, charpool));

            RPCJsonMvcOptionsSetup.ConfigureMvc(options, settings, null, charpool, null);

            Assert.AreEqual(1, options.OutputFormatters.Count);
            Assert.AreEqual(typeof(RPCJsonOutputFormatter), options.OutputFormatters[0].GetType());
        }
    }
}
