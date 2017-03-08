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
            blockSignaler = new Mock<ISignaler<Block>>();
            transactionSignaler = new Mock<ISignaler<Transaction>>();
            signals = new Signals(blockSignaler.Object, transactionSignaler.Object);
        }

        [Fact]
        public void SignalBlockBroadcastsToBlockSignaler()
        {
            var block = new Block();

            signals.Signal(block);

            blockSignaler.Verify(b => b.Broadcast(block), Times.Exactly(1));            
        }

        [Fact]
        public void SignalTransactionBroadcastsToTransactionSignaler()
        {
            var transaction = new Transaction();

            signals.Signal(transaction);
            
            transactionSignaler.Verify(b => b.Broadcast(transaction), Times.Exactly(1));
        }
    }
}