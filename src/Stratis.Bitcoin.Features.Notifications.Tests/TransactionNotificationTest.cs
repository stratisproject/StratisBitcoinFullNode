using System;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Signals;
using Xunit;

namespace Stratis.Bitcoin.Features.Notifications.Tests
{
    public class TransactionNotificationTest
    {
        [Fact]
        public void Notify_WithTransaction_BroadcastsSuccessfully()
        {
            var signals = new Mock<ISignals>();

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(new Transaction());
            signals.Verify(s => s.SignalTransaction(It.IsAny<Transaction>()), Times.Once);
        }

        [Fact]
        public void Notify_WithNullTransaction_DoesntBroadcast()
        {
            var signals = new Mock<ISignals>();

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(null);
            signals.Verify(s => s.SignalTransaction(It.IsAny<Transaction>()), Times.Never);
        }

        [Fact]
        public void NullSignalsThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => new TransactionNotification(null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }
    }
}