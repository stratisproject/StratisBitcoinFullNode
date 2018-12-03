using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using NBitcoin;

using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.FederatedPeg.IntegrationTests.Utils
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
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            SidechainNodeBuilder builder = new SidechainNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreateSidechainNode(Network network)
        {
            var agentName = $"sidechain{Interlocked.Increment(ref agentCount)}";
            return this.CreateNode(new SidechainNodeRunner(this.GetNextDataFolderName(agentName), agentName, network, this.TimeProvider), "poa.conf");
        }

        public CoreNode CreateSidechainNode(Network network, Key key)
        {
            var agentName = $"sidechain{Interlocked.Increment(ref agentCount)}";
            string dataFolder = this.GetNextDataFolderName(agentName);
            CoreNode node = this.CreateNode(new SidechainNodeRunner(dataFolder, agentName, network, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }
    }
}
