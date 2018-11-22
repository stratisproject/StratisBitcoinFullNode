using System;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    public interface IMockChain : IDisposable
    {
        void WaitForAllNodesToSync();
    }
}
