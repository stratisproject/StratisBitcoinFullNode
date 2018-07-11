using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class NodeConfigParameters : Dictionary<string, string>
    {
        public void Import(NodeConfigParameters configParameters)
        {
            foreach (KeyValuePair<string, string> kv in configParameters)
            {
                if (!this.ContainsKey(kv.Key))
                    this.Add(kv.Key, kv.Value);
            }
        }

        public void SetDefaultValueIfUndefined(string key, string value)
        {
            if(!this.ContainsKey(key)) this.Add(key, value);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in this)
                builder.AppendLine(kv.Key + "=" + kv.Value);
            return builder.ToString();
        }

        public string[] AsConsoleArgArray()
        {
            return this.Select(p => $"-{p.Key}={p.Value}").ToArray();
        }
    }
}