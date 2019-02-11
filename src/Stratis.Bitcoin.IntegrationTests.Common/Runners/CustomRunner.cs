using System;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class CustomNodeRunner : NodeRunner
    {
        private readonly Action<IFullNodeBuilder> callback;
        private readonly ProtocolVersion protocolVersion;
        private readonly ProtocolVersion minProtocolVersion;
        private readonly NodeConfigParameters configParameters;

        public CustomNodeRunner(string dataDir, Action<IFullNodeBuilder> callback, Network network,
            ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, NodeConfigParameters configParameters = null, string agent = "Custom",
            ProtocolVersion minProtocolVersion = ProtocolVersion.PROTOCOL_VERSION)
            : base(dataDir, agent)
        {
            this.callback = callback;
            this.Network = network;
            this.protocolVersion = protocolVersion;
            this.configParameters = configParameters ?? new NodeConfigParameters();
            this.minProtocolVersion = minProtocolVersion;
        }

        public override void BuildNode()
        {
            var argsAsStringArray = this.configParameters.AsConsoleArgArray();

            NodeSettings settings = null;

            if (string.IsNullOrEmpty(this.Agent))
                settings = new NodeSettings(this.Network, this.protocolVersion, args: argsAsStringArray) { MinProtocolVersion = this.minProtocolVersion };
            else
                settings = new NodeSettings(this.Network, this.protocolVersion, this.Agent, argsAsStringArray) { MinProtocolVersion = this.minProtocolVersion };

            IFullNodeBuilder builder = new FullNodeBuilder().UseNodeSettings(settings);

            this.callback(builder);

            builder.RemoveImplementation<PeerConnectorDiscovery>();
            builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());

            this.FullNode = (FullNode)builder.Build();
        }
    }
}