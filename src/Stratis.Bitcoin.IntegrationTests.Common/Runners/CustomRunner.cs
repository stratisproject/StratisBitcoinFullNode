using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class CustomNodeRunner : NodeRunner
    {
        private readonly string agent;
        private readonly Action<IFullNodeBuilder> callback;
        private readonly string configFileName;
        private readonly Network network;
        private readonly ProtocolVersion protocolVersion;
        private NodeConfigParameters args;

        public CustomNodeRunner(string dataDir, Action<IFullNodeBuilder> callback, Network network, 
            ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, NodeConfigParameters args = null, string agent = "Custom")
            : base(dataDir)
        {
            this.callback = callback;
            this.network = network;
            this.protocolVersion = protocolVersion;
            this.agent = agent;
            this.args = args ?? new NodeConfigParameters();
        }

        public override void BuildNode()
        {
            var argsAsStringArray = this.args.Where(p => p.Key != "conf").Select(p => $"-{p.Key}={p.Value}").ToArray();
            var settings = new NodeSettings(this.network, this.protocolVersion, this.agent, argsAsStringArray);
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