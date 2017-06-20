using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Notifications;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Notifications
{    
    [TestClass]
    public class TransactionNotificationTest
    {
        [TestMethod]
        public void NotifyWithTransactionBroadcastsSuccessfully()
        {
            var signals = new Mock<ISignals>();
            var signalerMock = new Mock<ISignaler<Transaction>>();
            signals.Setup(s => s.Transactions).Returns(signalerMock.Object);

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(new Transaction());
            signalerMock.Verify(s => s.Broadcast(It.IsAny<Transaction>()), Times.Once);

        }

        [TestMethod]
        public void NotifyWithNullTransactionDoesntBroadcast()
        {
            var signals = new Mock<ISignals>();
            var signalerMock = new Mock<ISignaler<Transaction>>();
            signals.Setup(s => s.Transactions).Returns(signalerMock.Object);

            var notification = new TransactionNotification(signals.Object);
            notification.Notify(null);
            signalerMock.Verify(s => s.Broadcast(It.IsAny<Transaction>()), Times.Never);

        }

        [TestMethod]
        public void NullSignalsThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new TransactionNotification(null));            
        }
    }
}
