using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Logging
{
    public static class LogsExtention
    {
        public static void AddFilters(this ILoggerFactory loggerFactory, LogSettings settings)
        {
            // TODO: preload enough args for -conf= or -datadir= to get debug args from there
            // TODO: currently only takes -debug arg

            var keyToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                //{ "addrman", "" },
                //{ "alert", "" },
                { "bench", "Stratis.Bitcoin.FullNode.ConsensusStats" },
                //{ "coindb", "" },
                { "db", "Stratis.Bitcoin.BlockStore" }, 
                //{ "lock", "" }, 
                //{ "rand", "" }, 
                { "rpc", "Stratis.Bitcoin.RPC" }, 
                //{ "selectcoins", "" }, 
                { "mempool", "Stratis.Bitcoin.MemoryPool" }, 
                //{ "mempoolrej", "" }, 
                { "net", "Stratis.Bitcoin.Connection" }, 
                //{ "proxy", "" }, 
                //{ "prune", "" }, 
                //{ "http", "" }, 
                //{ "libevent", "" }, 
                //{ "tor", "" }, 
                //{ "zmq", "" }, 
                //{ "qt", "" },

                // Short Names
                { "estimatefee", "Stratis.Bitcoin.Fee" },
                { "configuration", "Stratis.Bitcoin.Configuration" },
                { "fullnode", "Stratis.Bitcoin.FullNode" },
                { "consensus", "Stratis.Bitcoin.FullNode" },
                { "mining", "Stratis.Bitcoin.FullNode" },
                { "wallet", "Stratis.Bitcoin.Wallet" },
            };

            
            var filterSettings = new FilterLoggerSettings();
            // Default level is Information
            filterSettings.Add("Default", settings.LogLevel);
            // TODO: Probably should have a way to configure these as well
            filterSettings.Add("System", LogLevel.Warning);
            filterSettings.Add("Microsoft", LogLevel.Warning);
            // Disable aspnet core logs (retained from ASP.NET config)
            filterSettings.Add("Microsoft.AspNetCore", LogLevel.Error);

            if (settings.DebugArgs.Any())
            {
                if (settings.DebugArgs[0] == "1")
                {
                    // Increase all logging to Trace
                    filterSettings.Add("Stratis.Bitcoin", LogLevel.Trace);
                }
                else
                {
                    // Increase selected categories to Trace
                    foreach (var key in settings.DebugArgs)
                    {
                        string category;
                        if (keyToCategory.TryGetValue(key.Trim(), out category))
                        {
                            filterSettings.Add(category, LogLevel.Trace);
                        }
                        else
                        {
                            // Can directly specify something like -debug=Stratis.Bitcoin.Miner
                            filterSettings.Add(key, LogLevel.Trace);
                        }
                    }
                }
            }

            // TODO: Additional args
            //var logipsArgs = args.GetValueOf("-logips");
            //var printtoconsoleArgs = args.GetValueOf("-printtoconsole");

            loggerFactory.WithFilter(filterSettings);
        }

        public static void AddFile(this ILoggerFactory loggerFactory, DataFolder dataFolder)
        {
            loggerFactory.AddFile(
                Path.Combine(dataFolder.LogPath, "node-{Date}.json"), 
                isJson: true, 
                minimumLevel: LogLevel.Trace, 
                fileSizeLimitBytes: 5000000);
        }

        public const int ColumnLength = 16;
    }
}
