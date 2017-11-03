using System;

namespace Stratis.Bitcoin.Features.Api.Models
{
    public class KeepaliveMonitor
    {
        public DateTime LastBeat { get; set; }
        public TimeSpan KeepaliveInterval { get; set; }
    }
}
