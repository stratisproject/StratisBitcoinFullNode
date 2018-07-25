using Moq;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Signals
{
    public class SignalsTest
    {
        private readonly Mock<ISignaler<Block>> blockSignaler;
        private readonly Mock<ISignaler<ChainedHeader>> blockReorgSignaler;
        private readonly Bitcoin.Signals.Signals signals;
        private readonly Mock<ISignaler<Transaction>> transactionSignaler;

        public SignalsTest()
        {
            this.blockSignaler = new Mock<ISignaler<Block>>();
            this.blockReorgSignaler = new Mock<ISignaler<ChainedHeader>>();
            this.transactionSignaler = new Mock<ISignaler<Transaction>>();
            this.signals = new Bitcoin.Signals.Signals(this.blockSignaler.Object, this.blockReorgSignaler.Object, this.transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            Block block = Network.StratisMain.CreateBlock();

            this.signals.SignalBlock(block);

            this.blockSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));
        }

        [Fact]
        public void SignalBlockReorgBroadcastsToBlockSignaler()
        {
            var header = ChainedHeadersHelper.CreateGenesisChainedHeader();

            this.signals.SignalReorgedBlock(header);

            this.blockReorgSignaler.Verify(b => b.Broadcast(header), Times.Exactly(1));
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            Transaction transaction = Network.StratisMain.CreateTransaction();

            this.signals.SignalTransaction(transaction);

            this.transactionSignaler.Verify(b => b.Broadcast(transaction), Times.Exactly(1));
        }
    }
}