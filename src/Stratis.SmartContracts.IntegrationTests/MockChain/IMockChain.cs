using System;
using NBitcoin;

namespace Stratis.SmartContracts.IntegrationTests.MockChain
{
    public interface IMockChain : IDisposable
    {
        Network Network { get; }

        void WaitForAllNodesToSync();
    }
}
