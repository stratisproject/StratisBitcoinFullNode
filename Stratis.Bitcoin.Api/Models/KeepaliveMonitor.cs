using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Breeze.Api.Models
{
    public class KeepaliveMonitor
    {
        public DateTime LastBeat { get; set; }
        public TimeSpan KeepaliveInterval { get; set; }
    }
}
