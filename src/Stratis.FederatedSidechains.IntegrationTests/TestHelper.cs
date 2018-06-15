using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedSidechains.IntegrationTests
{
    public class TestHelper : Bitcoin.IntegrationTests.Common.TestHelper
    {
        public static void BuildStartAndRegisterNode(NodeBuilder nodeBuilder, 
            Action<IFullNodeBuilder> buildNodeAction,
            NodeKey nodeKey, 
            IDictionary<NodeKey, CoreNode> nodesByKey, 
            Network network,
            Action<CoreNode> addParameters = null, 
            ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION)
        {
            var node = nodeBuilder.CreateCustomNode(
                false, 
                buildNodeAction, 
                network, 
                agent: nodeKey.Name, 
                protocolVersion: protocolVersion);

            addParameters?.Invoke(node);

            nodesByKey.Add(nodeKey, node);
            node.Start();
            node.NotInIBD();

            WaitLoop(() => node.State == CoreNodeState.Running);
        }

        public static void ConnectNodeToOtherNodesInTest(NodeKey key, Dictionary<NodeKey, CoreNode> nodesByKey)
        {
            var thisNode = nodesByKey[key];
            var otherNodes = nodesByKey.Where(kvp => kvp.Key.Chain == key.Chain && kvp.Key.Name != key.Name).ToList();
            otherNodes.ForEach(o => thisNode.FullNode.ConnectionManager.AddNodeAddress(o.Value.Endpoint));
        }
    }
}
