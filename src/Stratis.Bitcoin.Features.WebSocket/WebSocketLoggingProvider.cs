using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketLoggingProvider : ILoggerProvider
    {
        private readonly WebSocketLoggerConfiguration config;
        private readonly ConcurrentDictionary<string, WebSocketLogger> loggers = new ConcurrentDictionary<string, WebSocketLogger>();
        private readonly IWebSocketService service;

        public WebSocketLoggingProvider(IWebSocketService service, WebSocketLoggerConfiguration config)
        {
            this.config = config;
            this.service = service;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return this.loggers.GetOrAdd(categoryName, name => new WebSocketLogger(name, this.service, this.config));
        }

        public void Dispose()
        {
            this.loggers.Clear();
        }
    }
}
