using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.FederatedSidechains.AdminDashboard.Models
{
    public class LogRule
    {
        public string Name { get; set; }
        public LogLevel MinLevel { get; set; } = LogLevel.Trace;
        public string Filename { get; set; }
    }

    public enum LogLevel
    {
        Trace,
        Informations,
        Warning,
        Error,
        Critical
    }
}
