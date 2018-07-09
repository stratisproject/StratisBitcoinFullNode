using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Features.RPC
{
    //{"code":-32601,"message":"Method not found"}
    public class RPCError
    {
        internal RPCError(JObject error)
        {
            this.Code = (RPCErrorCode)((int)error.GetValue("code"));
            this.Message = (string)error.GetValue("message");
        }
        public RPCErrorCode Code
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        }
    }
    //{"result":null,"error":{"code":-32601,"message":"Method not found"},"id":1}
    public class RPCResponse
    {
        public RPCResponse(JObject json)
        {
            var error = json.GetValue("error") as JObject;
            if(error != null)
            {
                this.Error = new RPCError(error);
            }

            this.Result = json.GetValue("result") as JToken;
        }
        public RPCError Error
        {
            get;
            set;
        }

        public JToken Result
        {
            get;
            set;
        }

        public string ResultString
        {
            get
            {
                if(this.Result == null)
                    return null;
                return this.Result.ToString();
            }
        }

        public static RPCResponse Load(Stream stream)
        {
            var reader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8));
            return new RPCResponse(JObject.Load(reader));
        }

        public void ThrowIfError()
        {
            if(this.Error != null)
            {
                throw new RPCException(this.Error.Code, this.Error.Message, this);
            }
        }
    }
}
