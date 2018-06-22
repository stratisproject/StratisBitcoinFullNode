using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Controller providing access to logs in the browser
    /// </summary>
    [Route("")]
    [Route("[controller]")]
    public class LogsController : Controller
    {
        private readonly NodeSettings nodeSettings;

        public LogsController(NodeSettings nodeSettings)
        {
            this.nodeSettings = nodeSettings;
        }

        /// <summary>
        /// Lists the logs available
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("Logs")]
        public IActionResult Logs()
        {
            string logPath = Path.Combine(this.nodeSettings.DataDir, @"Logs");

            if (!Directory.Exists(logPath))
                return this.Content($"There is no directory at {logPath}.");

            return this.Content(string.Join("<br/><br/>", Directory.GetFiles(logPath).Select(x => $"<a href='/Logs/{Path.GetFileNameWithoutExtension(x)}'>{x}</a>")), "text/html");
        }

        /// <summary>
        /// Returns a web page view over the any log file in the data directory
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("Logs/{logfileName}/{numberOfLogEntriesToShow?}")]
        public IActionResult Logs(string logfileName, int numberOfLogEntriesToShow = 30)
        {
            string logPath = Path.Combine(this.nodeSettings.DataDir, @"Logs", $"{logfileName}.txt");

            if (!System.IO.File.Exists(logPath))
            {
                return this.Content($"There is no log file at: {logPath}. An nlog.config file is needed in the daemon directory.");
            }

            string[] logLines = System.IO.File.ReadAllLines(logPath);

            int entriesToSkip = logLines.Length < numberOfLogEntriesToShow ? 0 : logLines.Length - numberOfLogEntriesToShow;

            return this.Content(string.Join("\r\n", logLines.Skip(entriesToSkip)));
        }
    }
}