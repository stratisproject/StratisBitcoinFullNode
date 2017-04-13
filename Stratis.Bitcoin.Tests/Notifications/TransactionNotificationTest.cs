using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Notifications;
using Xunit;

namespace Stratis.Bitcoin.Tests.Notifications
{    
    public class TransactionNotificationTest
    {
        [Fact]
        public void NotifyWithTransactionBroadcastsSuccessfully()
        {
            var signals = new Mock<ISignals>();
            var signalerMock = new Mock<ISignaler<Transaction>>();
            signals.Setup(s => s.Transactions).Returns(signalerMock.Object);

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(new Transaction());
            signalerMock.Verify(s => s.Broadcast(It.IsAny<Transaction>()), Times.Once);

        }

        [Fact]
        public void NotifyWithNullTransactionDoesntBroadcast()
        {
            var signals = new Mock<ISignals>();
            var signalerMock = new Mock<ISignaler<Transaction>>();
            signals.Setup(s => s.Transactions).Returns(signalerMock.Object);

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(null);
            signalerMock.Verify(s => s.Broadcast(It.IsAny<Transaction>()), Times.Never);

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
