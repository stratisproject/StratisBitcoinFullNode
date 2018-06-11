using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.FederatedSidechains.IntegrationTests.Common
{
    public static class CoreNodeExtensions
    {
        public static int ApiPort(this CoreNode coreNode)
        {
            return coreNode.FullNode.NodeService<ApiSettings>().ApiPort;
        }
    }
}
