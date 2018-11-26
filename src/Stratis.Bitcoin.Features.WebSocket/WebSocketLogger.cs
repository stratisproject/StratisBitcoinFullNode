using System;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketLogger : ILogger
    {
        private readonly string name;
        private readonly WebSocketLoggerConfiguration config;
        private readonly IWebSocketService service;

        public WebSocketLogger(string name, IWebSocketService service, WebSocketLoggerConfiguration config)
        {
            this.name = name;
            this.config = config;
            this.service = service;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == this.config.LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (!this.name.StartsWith("Stratis"))
            {
                return;
            }

            // Send log to Web Socket clients.
            var logLine = $"{logLevel.ToString()} - {eventId.Id} - {this.name} - {formatter(state, exception)}";
            this.service.Broadcast(logLine);
            //this.service.

            Console.WriteLine(logLine);

            //if (config.EventId == 0 || config.EventId == eventId.Id)
            //{
            //    var color = Console.ForegroundColor;
            //    Console.ForegroundColor = config.Color;
            //    Console.WriteLine($"{logLevel.ToString()} - {eventId.Id} - {name} - {formatter(state, exception)}");
            //    Console.ForegroundColor = color;
            //}
        }
    }
}
