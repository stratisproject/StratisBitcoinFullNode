using System;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.FederatedSidechains.IntegrationTests.Common
{
    public static class NodeBuilderExtensions
    {
        public static CoreNode CreateCustomNodeWithFreeApiPort(this NodeBuilder nodeBuilder, bool start, Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, string configFileName = "custom.conf", string agent = "Custom")
        {
            var currentPort = nodeBuilder.ConfigParameters.TryGetValue("apiport", out string currentPortAsString) ? int.Parse(currentPortAsString) : 0;
            var freePort = new [] {currentPort};
            TestHelper.FindPorts(freePort);
            nodeBuilder.ConfigParameters["apiport"] = freePort[0].ToString();
            var node = nodeBuilder.CreateCustomNode(start, callback, network, protocolVersion, configFileName, agent);
            return node;
        }
    }
}