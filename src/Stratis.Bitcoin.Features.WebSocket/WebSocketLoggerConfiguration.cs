using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Trace;

        //public int EventId { get; set; } = 0;
        //public ConsoleColor Color { get; set; } = ConsoleColor.Yellow;
    }
}
