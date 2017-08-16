using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Signals;
using Xunit;

namespace Stratis.Bitcoin.Tests.Notifications
{    
    public class TransactionNotificationTest
    {
        [Fact]
        public void NotifyWithTransactionBroadcastsSuccessfully()
        {
            var signals = new Mock<ISignals>();

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(new Transaction());
            signals.Verify(s => s.SignalTransaction(It.IsAny<Transaction>()), Times.Once);
        }

        [Fact]
        public void NotifyWithNullTransactionDoesntBroadcast()
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
