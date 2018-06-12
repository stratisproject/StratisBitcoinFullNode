using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var sw = new StringWriter();
            formatter.WriteObject(sw, model);
            string json = sw.ToString();
            return json;
        }
    }
}
