﻿using System.IO;
using System.Runtime.CompilerServices;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools
{
    public class PoANodeBuilder : NodeBuilder
    {
        private PoANodeBuilder(string rootFolder) : base(rootFolder)
        {
        }

        public static PoANodeBuilder CreatePoANodeBuilder(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            PoANodeBuilder builder = new PoANodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreatePoANode(PoANetwork network)
        {
            return this.CreateNode(new PoANodeRunner(this.GetNextDataFolderName(), network), "poa.conf");
        }

        public CoreNode CreatePoANode(PoANetwork network, Key key)
        {
            string dataFolder = this.GetNextDataFolderName();
            CoreNode node = this.CreateNode(new PoANodeRunner(dataFolder, network), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }
    }
}
