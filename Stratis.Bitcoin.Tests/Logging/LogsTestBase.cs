using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.Tests.Logging
{
	public class LogsTestBase
	{
		private Mock<ILogger> fullNodeLogger;
		private Mock<ILoggerFactory> loggerFactory;
		private Mock<ILogger> rpcLogger;

		/// <remarks>
		/// This class is not able to work concurrently because logs is a static class.
		/// The logs class needs to be refactored first before tests can run in parallel.
		/// </remarks>
		public LogsTestBase()
		{
			fullNodeLogger = new Mock<ILogger>();
			rpcLogger = new Mock<ILogger>();
			loggerFactory = new Mock<ILoggerFactory>();
			loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
			   .Returns(new Mock<ILogger>().Object);
			loggerFactory.Setup(l => l.CreateLogger("FullNode"))
			   .Returns(fullNodeLogger.Object)
			   .Verifiable();
			loggerFactory.Setup(l => l.CreateLogger("RPC"))
			   .Returns(rpcLogger.Object)
			   .Verifiable();
			Logs.Configure(loggerFactory.Object);
		}

		public Mock<ILogger> FullNodeLogger
		{
			get
			{
				return fullNodeLogger;
			}
		}

		public Mock<ILogger> RPCLogger
		{
			get
			{
				return rpcLogger;
			}
		}

		protected void AssertLog<T>(Mock<ILogger> logger, LogLevel logLevel, string exceptionMessage, string message) where T : Exception
		{
			logger.Verify(f => f.Log<Object>(logLevel,
				It.IsAny<EventId>(),
				It.Is<Object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
				It.Is<T>(t => t.Message.Equals(exceptionMessage)),
				It.IsAny<Func<Object, Exception, string>>()));
		}

		protected void AssertLog(Mock<ILogger> logger, LogLevel logLevel, string message)
		{
			logger.Verify(f => f.Log<Object>(logLevel,
				It.IsAny<EventId>(),
				It.Is<Object>(l => ((FormattedLogValues)l)[0].Value.ToString().EndsWith(message)),
				null,
				It.IsAny<Func<Object, Exception, string>>()));
		}
	}
}
