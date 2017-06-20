using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using Stratis.Bitcoin.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Logging
{
    [TestClass]
    public class LogsTestBase
    {
        private Mock<ILogger> fullNodeLogger;
        private Mock<ILoggerFactory> loggerFactory;
        private Mock<ILogger> rpcLogger;

        /// <remarks>
        /// This class is not able to work concurrently because logs is a static class.
        /// The logs class needs to be refactored first before tests can run in parallel.
        /// </remarks>
        [TestInitialize]
        public void InitializeBase()
        {
            this.fullNodeLogger = new Mock<ILogger>();
            this.rpcLogger = new Mock<ILogger>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.FullNode"))
               .Returns(this.fullNodeLogger.Object)
               .Verifiable();
            this.loggerFactory.Setup(l => l.CreateLogger("Stratis.Bitcoin.RPC"))
               .Returns(this.rpcLogger.Object)
               .Verifiable();
            Logs.Configure(this.loggerFactory.Object);

            this.Initialize();
        }

        protected virtual void Initialize()
        {
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

        protected void AssertLog<T>(Mock<ILogger> logger, LogLevel logLevel, string exceptionMessage, string message) where T : Exception
        {
            logger.Verify(f => f.Log<object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                It.Is<T>(t => t.Message.Equals(exceptionMessage)),
                It.IsAny<Func<object, Exception, string>>()));
        }

        protected void AssertLog(Mock<ILogger> logger, LogLevel logLevel, string message)
        {
            logger.Verify(f => f.Log<object>(logLevel,
                It.IsAny<EventId>(),
                It.Is<object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
                null,
                It.IsAny<Func<object, Exception, string>>()));
        }
    }
}
