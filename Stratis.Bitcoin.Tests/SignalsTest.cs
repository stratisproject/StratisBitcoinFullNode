using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class SignalsTest
    {
        private Mock<ISignaler<Block>> blockSignaler;
        private Signals signals;
        private Mock<ISignaler<Transaction>> transactionSignaler;

        public SignalsTest()
        {
            this.blockSignaler = new Mock<ISignaler<Block>>();
            this.transactionSignaler = new Mock<ISignaler<Transaction>>();
            this.signals = new Signals(this.blockSignaler.Object, this.transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            var block = new Block();

            this.signals.Signal(block);

            this.blockSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));            
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            var transaction = new Transaction();

            this.signals.Signal(transaction);

            this.transactionSignaler.Verify(b => b.Broadcast(transaction), Times.Exactly(1));
        }
    }
}