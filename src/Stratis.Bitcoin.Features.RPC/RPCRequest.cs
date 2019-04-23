using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCRequest
    {
        public RPCRequest(RPCOperations method, object[] parameters)
            : this(method.ToString(), parameters)
        {

        }
        public RPCRequest(string method, object[] parameters)
            : this()
        {
            this.Method = method;
            this.Params = parameters;
        }
        public RPCRequest()
        {
            this.JsonRpc = "1.0";
        }
        public string JsonRpc
        {
            get;
            set;
        }
        public string Id
        {
            get;
            set;
        }
        public string Method
        {
            get;
            set;
        }
        public object[] Params
        {
            get;
            set;
        }

        public void WriteJSON(TextWriter writer)
        {
            var jsonWriter = new JsonTextWriter(writer);
            WriteJSON(jsonWriter);
            jsonWriter.Flush();
        }

        internal void WriteJSON(JsonTextWriter writer)
        {
            writer.WriteStartObject();
            WriteProperty(writer, "jsonrpc", this.JsonRpc);
            WriteProperty(writer, "id", this.Id);
            WriteProperty(writer, "method", this.Method);

            writer.WritePropertyName("params");
            writer.WriteStartArray();

            if(this.Params != null)
            {
                for(int i = 0; i < this.Params.Length; i++)
                {
                    if(this.Params[i] is JToken)
                    {
                        ((JToken) this.Params[i]).WriteTo(writer);
                    }
                    else if(this.Params[i] is Array)
                    {
                        writer.WriteStartArray();
                        foreach(object x in (Array) this.Params[i])
                        {
                            writer.WriteValue(x);
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        writer.WriteValue(this.Params[i]);
                    }
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private void WriteProperty<TValue>(JsonTextWriter writer, string property, TValue value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }
    }
}
