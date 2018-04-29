using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.FederatedPeg.Features.FederationGateway.Notifications
{
    /// <summary>
    /// Observer that passes notifications indicating the arrival of new <see cref="Block"/>s
    /// onto the CrossChainTransactionMonitor.
    /// </summary>
    internal sealed class BlockObserver : SignalObserver<Block>
    {
        private readonly ICrossChainTransactionMonitor crossChainTransactionMonitor;

        public BlockObserver(ICrossChainTransactionMonitor crossChainTransactionMonitor)
        {
            this.crossChainTransactionMonitor = crossChainTransactionMonitor;
        }

        /// <summary>
        /// When a block is received it is passed to the monitor.
        /// </summary>
        /// <param name="block">The new block.</param>
        protected override void OnNextCore(Block block)
        {
            this.crossChainTransactionMonitor.ProcessBlock(block);
        }
    }
}