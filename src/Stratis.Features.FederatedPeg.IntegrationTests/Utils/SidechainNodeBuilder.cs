using System.Runtime.CompilerServices;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    public class SidechainNodeBuilder : NodeBuilder
    {
        public EditableTimeProvider TimeProvider { get; }

        public static int agentCount;

        private SidechainNodeBuilder(string rootFolder) : base(rootFolder)
        {
            this.TimeProvider = new EditableTimeProvider();
        }

        public static SidechainNodeBuilder CreateSidechainNodeBuilder(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = Bitcoin.Tests.Common.TestBase.CreateTestDir(caller, callingMethod);
            var builder = new SidechainNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreateSidechainNode(Network network)
        {
            string agentName = $"sideuser{Interlocked.Increment(ref agentCount)}";
            return this.CreateNode(new SidechainUserNodeRunner(this.GetNextDataFolderName(agentName), agentName, network, this.TimeProvider), "poa.conf");
        }

        public CoreNode CreateSidechainFederationNode(Network network, Network counterChainNetwork, Key key, bool testingFederation = true)
        {
            string agentName = $"sidefed{Interlocked.Increment(ref agentCount)}";
            string dataFolder = this.GetNextDataFolderName(agentName);
            CoreNode node = this.CreateNode(new SidechainFederationNodeRunner(dataFolder, agentName, network, counterChainNetwork, testingFederation, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }

        public CoreNode CreateMainChainFederationNode(Network network, Network counterChainNetwork)
        {
            string agentName = $"mainfed{Interlocked.Increment(ref agentCount)}";
            string dataFolder = this.GetNextDataFolderName(agentName);
            CoreNode node = this.CreateNode(new MainChainFederationNodeRunner(dataFolder, agentName, network, counterChainNetwork), "stratis.conf");

            return node;
        }
    }
}
