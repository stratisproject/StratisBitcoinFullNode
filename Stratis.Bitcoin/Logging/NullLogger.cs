using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Logging
{
	public class NullLogger : ILogger
	{
		class Dispo : IDisposable
		{
			public void Dispose()
			{
				
			}
		}
		public IDisposable BeginScope<TState>(TState state)
		{
			return new Dispo();
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			
		}
	}
}
