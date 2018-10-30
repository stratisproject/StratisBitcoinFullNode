﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class SmartContractNodeBuilder : NodeBuilder
    {
        public EditableTimeProvider TimeProvider { get; }

        public SmartContractNodeBuilder(string rootFolder) : base(rootFolder)
        {
            this.TimeProvider = new EditableTimeProvider();
        }

        public CoreNode CreateSmartContractPoANode(Key key)
        {
            Network network = new SmartContractsPoARegTest();
            string dataFolder = this.GetNextDataFolderName();
            CoreNode node = this.CreateNode(new SmartContractPoARunner(dataFolder, network, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key);

            return node;
        }

        public CoreNode CreateSmartContractPowNode()
        {
            Network network = new SmartContractsRegTest();
            return CreateNode(new StratisSmartContractNode(this.GetNextDataFolderName(), network), "stratis.conf");
        }

        public CoreNode CreateSmartContractPosNode()
        {
            Network network = new SmartContractPosRegTest();
            return CreateNode(new StratisSmartContractPosNode(this.GetNextDataFolderName(), network), "stratis.conf");
        }

        public static SmartContractNodeBuilder Create(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            var builder = new SmartContractNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();
            return builder;
        }
    }
}
