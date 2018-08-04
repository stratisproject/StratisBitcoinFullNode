using Moq;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalsTest
    {
        private readonly Mock<ISignaler<Block>> blockConnectedSignaler;
        private readonly Mock<ISignaler<Block>> blockDisconnectedSignaler;
        private readonly Bitcoin.Signals.Signals signals;
        private readonly Mock<ISignaler<Transaction>> transactionSignaler;

        public SignalsTest()
        {
            this.blockConnectedSignaler = new Mock<ISignaler<Block>>();
            this.blockDisconnectedSignaler = new Mock<ISignaler<Block>>();
            this.transactionSignaler = new Mock<ISignaler<Transaction>>();
            this.signals = new Bitcoin.Signals.Signals(this.blockConnectedSignaler.Object, this.blockDisconnectedSignaler.Object, this.transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();

            this.signals.SignalBlockConnected(block);

            this.blockConnectedSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));
        }

        [Fact]
        public void SignalBlockDisconnectedToBlockSignaler()
        {
            Block block = KnownNetworks.StratisMain.CreateBlock();

            this.signals.SignalBlockDisconnected(block);

            this.blockDisconnectedSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));
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