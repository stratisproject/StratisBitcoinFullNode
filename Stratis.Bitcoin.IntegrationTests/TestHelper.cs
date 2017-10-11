using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class TestHelper
    {
        public static async Task WaitLoopAsync(Func<bool> act)
        {
            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while(!act())
            {
                cancel.Token.ThrowIfCancellationRequested();
                await Task.Delay(50).ConfigureAwait(false);
            }
        }
        public static async Task WaitLoopAsync(Func<Task<bool>> act)
        {
            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while (!await act().ConfigureAwait(false))
            {
                cancel.Token.ThrowIfCancellationRequested();
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2)
        {
            if (node1.FullNode.Chain.Tip.HashBlock != node2.FullNode.Chain.Tip.HashBlock) return false;
            if (node1.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock != node2.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock) return false;
            if (node1.FullNode.HighestPersistedBlock().HashBlock != node2.FullNode.HighestPersistedBlock().HashBlock) return false;
            if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count) return false;
            if (node1.FullNode.WalletManager().WalletTipHash != node2.FullNode.WalletManager().WalletTipHash) return false;
            if (node1.CreateRPCClient().GetBestBlockHash() != node2.CreateRPCClient().GetBestBlockHash()) return false;
            return true;
        }

        public static bool IsNodeSynced(CoreNode node)
        {
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.HighestPersistedBlock().HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.WalletManager().WalletTipHash) return false;
            return true;
        }
    }
}
