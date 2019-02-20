using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalsTest
    {
        private readonly Bitcoin.Signals.ISignals signals;

        public SignalsTest()
        {
            this.signals = new Bitcoin.Signals.Signals();
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);

            bool signaled = false;
            this.signals.OnBlockConnected.Attach(headerBlock => signaled = true);

            this.signals.OnBlockConnected.Notify(chainedHeaderBlock);

            Assert.True(signaled);
        }

        [Fact]
        public void SignalBlockDisconnectedToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);

            bool signaled = false;
            this.signals.OnBlockDisconnected.Attach(headerBlock => signaled = true);

            this.signals.OnBlockDisconnected.Notify(chainedHeaderBlock);

            Assert.True(signaled);
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();

            bool signaled = false;
            this.signals.OnTransactionReceived.Attach(transaction1 => signaled = true);

            this.signals.OnTransactionReceived.Notify(transaction);

            Assert.True(signaled);
        }
    }
}