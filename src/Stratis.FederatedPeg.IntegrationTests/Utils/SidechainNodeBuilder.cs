using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

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

        private SidechainNodeBuilder(string rootFolder) : base(rootFolder)
        {
            this.TimeProvider = new EditableTimeProvider();
        }

        public static SidechainNodeBuilder CreatePoANodeBuilder(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            SidechainNodeBuilder builder = new SidechainNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreatePoANode(PoANetwork network)
        {
            return this.CreateNode(new PoANodeRunner(this.GetNextDataFolderName(), network, this.TimeProvider), "poa.conf");
        }

        public CoreNode CreatePoANode(PoANetwork network, Key key)
        {
            string dataFolder = this.GetNextDataFolderName();
            CoreNode node = this.CreateNode(new PoANodeRunner(dataFolder, network, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }
    }
}
