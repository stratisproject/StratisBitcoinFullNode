using System;

namespace Stratis.SmartContracts.IntegrationTests.MockChain
{
    public interface IMockChain : IDisposable
    {
        void WaitForAllNodesToSync();
    }
}
