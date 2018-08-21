using Moq;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalsTest
    {
        private readonly Mock<ISignaler<ChainedHeaderBlock>> blockConnectedSignaler;
        private readonly Mock<ISignaler<ChainedHeaderBlock>> blockDisconnectedSignaler;
        private readonly Bitcoin.Signals.Signals signals;
        private readonly Mock<ISignaler<Transaction>> transactionSignaler;

        public SignalsTest()
        {
            this.blockConnectedSignaler = new Mock<ISignaler<ChainedHeaderBlock>>();
            this.blockDisconnectedSignaler = new Mock<ISignaler<ChainedHeaderBlock>>();
            this.transactionSignaler = new Mock<ISignaler<Transaction>>();
            this.signals = new Bitcoin.Signals.Signals(this.blockConnectedSignaler.Object, this.blockDisconnectedSignaler.Object, this.transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);
            
            this.signals.SignalBlockConnected(chainedHeaderBlock);

            this.blockConnectedSignaler.Verify(b => b.Broadcast(chainedHeaderBlock), Times.Exactly(1));
        }

        [Fact]
        public void SignalBlockDisconnectedToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();
            ChainedHeader header = ChainedHeadersHelper.CreateGenesisChainedHeader();
            var chainedHeaderBlock = new ChainedHeaderBlock(block, header);

            this.signals.SignalBlockDisconnected(chainedHeaderBlock);

            this.blockDisconnectedSignaler.Verify(b => b.Broadcast(chainedHeaderBlock), Times.Exactly(1));
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            Transaction transaction = KnownNetworks.StratisMain.CreateTransaction();

            this.signals.SignalTransaction(transaction);

            this.transactionSignaler.Verify(b => b.Broadcast(transaction), Times.Exactly(1));
        }
    }
}