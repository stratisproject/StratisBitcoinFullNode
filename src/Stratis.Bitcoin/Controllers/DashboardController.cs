using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Controller providing HTML Dashboard
    /// </summary>
    [Route("")]
    [Route("[controller]")]
    public class DashboardController : Controller
    {

        private readonly IFullNode fullNode;
        private NodeSettings nodeSettings;

        public DashboardController(IFullNode fullNode, NodeSettings nodeSettings)
        {
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
        }

        /// <summary>
        /// Returns a web page to act as a dashboard
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("")]
        [Route("Stats")]
        public IActionResult Stats()
        {
            string content = (this.fullNode as FullNode).LastLogOutput;
            return this.Content(content);
        }

        /// <summary>
        /// Returns a web page view over the SmartContract Logs
        /// </summary>
        /// <returns>text/html content</returns>
        [HttpGet]
        [Route("SmartContracts/{numberOfLogEntriesToShow?}")]
        public IActionResult SmartContractLogs(int numberOfLogEntriesToShow = 30)
        {
            string logPath = Path.Combine(this.nodeSettings.DataDir, @"Logs\smartcontracts.txt");

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