using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.IntegrationTests.MockChain
{
    public interface IMockChain : IDisposable
    {
        Network Network { get; }

        void WaitForAllNodesToSync();
    }
}
