using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.Bitcoin.Features.RPC.Tests.Models
{
    public class BaseRPCModelTest
    {
        protected static JObject ModelToJObject(object model)
        {
            string json = ModelToJson(model);
            JObject obj = JObject.Parse(json);
            return obj;
        }

        protected static string ModelToJson(object model)
        {
            var formatter = new RPCJsonOutputFormatter(new JsonSerializerSettings(), System.Buffers.ArrayPool<char>.Create());
            StringWriter sw = new StringWriter();
            formatter.WriteObject(sw, model);
            string json = sw.ToString();
            return json;
        }
    }
}
