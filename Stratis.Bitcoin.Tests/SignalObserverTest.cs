using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Logging;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class SignalObserverTest
    {
        SignalObserver<Block> observer;
        Mock<ILoggerFactory> loggerFactory;
        Mock<Microsoft.Extensions.Logging.ILogger> fullNodeLogger;

        public SignalObserverTest()
        {
            fullNodeLogger = new Mock<ILogger>();
            loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);
            loggerFactory.Setup(l=> l.CreateLogger("FullNode"))
               .Returns(fullNodeLogger.Object)
               .Verifiable();
            Logs.Configure(loggerFactory.Object);

            observer = new TestBlockSignalObserver();
        }

        [Fact]
        public void SignalObserverLogsSignalOnError()
        {
            var exception = new InvalidOperationException("This should not have occurred!");

            observer.OnError(exception);

            AssertLog(fullNodeLogger, LogLevel.Error, exception.ToString());
        }
       
        private void AssertLog(Mock<ILogger> logger, LogLevel logLevel, string message)
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<Object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                null,
                It.IsAny<Func<Object, Exception, string>>()));
        }

        private class TestBlockSignalObserver : SignalObserver<Block>
        {
            public TestBlockSignalObserver()
            {
            }

            protected override void OnNextCore(Block value)
            {

            }
        }
    }
}
