using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    public interface IMockChain : IDisposable
    {
        IReadOnlyList<MockChainNode> Nodes { get; }

        void WaitForAllNodesToSync();

        void MineBlocks(int num);

        void WaitAllMempoolCount(int num);
    }
}
