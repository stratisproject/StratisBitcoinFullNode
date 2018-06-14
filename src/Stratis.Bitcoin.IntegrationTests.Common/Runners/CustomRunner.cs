using System;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class CustomNodeRunner : NodeRunner
    {
        private readonly string agent;
        private readonly Action<IFullNodeBuilder> callback;
        private readonly string configFileName;
        private readonly Network network;
        private readonly ProtocolVersion protocolVersion;

        public CustomNodeRunner(string dataDir, Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, string configFileName = "custom.conf", string agent = "Custom")
            : base(dataDir)
        {
            this.callback = callback;
            this.network = network;
            this.protocolVersion = protocolVersion;
            this.configFileName = configFileName;
            this.agent = agent;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.network, this.protocolVersion, this.agent, new string[] { "-conf=" + this.configFileName, "-datadir=" + this.DataFolder });
            IFullNodeBuilder builder = new FullNodeBuilder().UseNodeSettings(settings);

            this.callback(builder);
            this.FullNode = (FullNode)builder.Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}