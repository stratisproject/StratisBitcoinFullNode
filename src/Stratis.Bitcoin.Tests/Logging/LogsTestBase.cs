using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;

namespace Stratis.Bitcoin.Tests.Logging
{
    public class LogsTestBase : TestBase
    {
        private Mock<ILogger> fullNodeLogger;
        private Mock<ILoggerFactory> mockLoggerFactory;
        private Mock<ILogger> rpcLogger;
        private Mock<ILogger> logger;

        /// <remarks>
        /// This class is not able to work concurrently because logs is a static class.
        /// The logs class needs to be refactored first before tests can run in parallel.
        /// </remarks>
        public LogsTestBase()
        {
            this.fullNodeLogger = new Mock<ILogger>();
            this.rpcLogger = new Mock<ILogger>();
            this.logger = new Mock<ILogger>();
            this.mockLoggerFactory = new Mock<ILoggerFactory>();
            this.mockLoggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(this.logger.Object);
            this.mockLoggerFactory.Setup(l => l.CreateLogger(typeof(FullNode).FullName))
               .Returns(this.fullNodeLogger.Object)
               .Verifiable();
            /* 
            // TODO: Re-factor by moving to Stratis.Bitcoin.Features.RPC.Tests or Stratis.Bitcoin.IntegrationTests
            this.mockLoggerFactory.Setup(l => l.CreateLogger(typeof(RPCFeature).FullName))
                .Returns(this.rpcLogger.Object)
                 .Verifiable();
            */
        }

        public Mock<ILoggerFactory> LoggerFactory
        {
            get
            {
                return this.mockLoggerFactory;
            }
        }

        public Mock<ILogger> FullNodeLogger
        {
            get
            {
                return this.fullNodeLogger;
            }
        }

        public Mock<ILogger> RPCLogger
        {
            get
            {
                return this.rpcLogger;
            }
        }

        public Mock<ILogger> Logger
        {
            get
            {
                return this.logger;
            }
        }

        protected void AssertLog<T>(Mock<ILogger> logger, LogLevel logLevel, string exceptionMessage, string message) where T : Exception
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                It.Is<T>(t => t.Message.Equals(exceptionMessage)),
                It.IsAny<Func<object, Exception, string>>()));
        }

        protected void AssertLog<T>(Mock<ILogger<FullNode>> logger, LogLevel logLevel, string exceptionMessage, string message) where T : Exception
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                It.Is<T>(t => t.Message.Equals(exceptionMessage)),
                It.IsAny<Func<object, Exception, string>>()));
        }

        protected void AssertLog(Mock<ILogger> logger, LogLevel logLevel, string message)
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                null,
                It.IsAny<Func<object, Exception, string>>()));
        }
        /* TODO: Re-factor
        protected void AssertLog(Mock<ILogger<RPCMiddleware>> logger, LogLevel logLevel, string message)
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                null,
                It.IsAny<Func<object, Exception, string>>()));
        }
        */
        protected void AssertLog(Mock<ILogger<FullNode>> logger, LogLevel logLevel, string message)
        {
            logger.Verify(f => f.Log<Object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                null,
                It.IsAny<Func<object, Exception, string>>()));
        }

    }
}
