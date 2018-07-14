﻿using System;
using System.Text;
using System.Timers;
using Stratis.Bitcoin.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.IO;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Configuration related to the API interface.
    /// </summary>
    public class DashboardApiSettings
    {
        /// <summary>The default port used by the API when the node runs on the bitcoin network.</summary>
        public const int DefaultBitcoinApiPort = 37220;

        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        public const int DefaultStratisApiPort = 37221;

        /// <summary>The default port used by the API when the node runs on the bitcoin testnet network.</summary>
        public const int TestBitcoinApiPort = 38220;

        /// <summary>The default port used by the API when the node runs on the Stratis testnet network.</summary>
        public const int TestStratisApiPort = 38221;

        public const int DefaultHttpPort = 4200;

        /// <summary>The default port used by the API when the node runs on the Stratis network.</summary>
        public const string DefaultApiHost = "http://localhost";

        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }

        /// <summary>Port of node's API interface.</summary>
        public int ApiPort { get; set; }

        /// <summary>URI to node's API interface.</summary>
        public Timer KeepaliveTimer { get; private set; }

        /// <summary>The callback used to override/constrain/extend the settings provided by the Load method.</summary>
        private Action<DashboardApiSettings> callback;

        public static NodeSettings NodeSettings {get; set;}

        /// <summary>
        /// Constructs this object whilst providing a callback to override/constrain/extend 
        /// the settings provided by the Load method.
        /// </summary>
        /// <param name="callback">The callback used to override/constrain/extend the settings provided by the Load method.</param>
        public DashboardApiSettings(Action<DashboardApiSettings> callback)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the API related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            TextFileConfiguration config = nodeSettings.ConfigReader;

            NodeSettings = nodeSettings;

            var apiHost = config.GetOrDefault("apiuri", DefaultApiHost);
            Uri apiUri = new Uri(apiHost);

            // Find out which port should be used for the API.
            var apiPort = GetDefaultPort();
            
            // If no port is set in the API URI.
            if (apiUri.IsDefaultPort)
            {
                this.ApiUri = new Uri($"{apiHost}:{apiPort}");
                this.ApiPort = apiPort;
            }
            // If a port is set in the -apiuri, it takes precedence over the default port or the port passed in -apiport.
            else
            {
                this.ApiUri = apiUri;
                this.ApiPort = apiUri.Port;
            }

            // Set the keepalive interval (set in seconds).
            var keepAlive = config.GetOrDefault("keepalive", 0);
            if (keepAlive > 0)
            {
                this.KeepaliveTimer = new Timer
                {
                    AutoReset = false,
                    Interval = keepAlive * 1000
                };
            }

            this.callback?.Invoke(this);
        }


        /// <summary>
        /// Get root directory
        /// </summary>
        /// <returns>The feature root directory.</returns>
        public static string GetRootDirectory()
        {
            var path = Directory.GetParent(Directory.GetCurrentDirectory()).FullName + "\\Stratis.Bitcoin.Features.Dashboard";
            if (Directory.Exists(path))
            {
                return path;
            } else
            {
                throw new Exception("Path not found " + path);
            }
        }

        /// <summary>
        /// Determines the default API port.
        /// </summary>
        /// <param name="network">The network to use.</param>
        /// <returns>The default API port.</returns>
        private static int GetDefaultPort()
        {
            return DefaultHttpPort;
        }

        /// <summary>Prints the help information on how to configure the API settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-apiuri=<string>          URI to node's API interface. Defaults to '{ DefaultApiHost }'.");
            builder.AppendLine($"-apiport=<0-65535>        Port of node's API interface. Defaults to { GetDefaultPort() }.");
            builder.AppendLine($"-keepalive=<seconds>      Keep Alive interval (set in seconds). Default: 0 (no keep alive).");

            NodeSettings.Default().Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Dashboard Settings####");
            builder.AppendLine($"#URI to node's API interface. Defaults to '{ DefaultApiHost }'");
            builder.AppendLine($"#apiuri={ DefaultApiHost }");
            builder.AppendLine($"#Port of node's API interface. Defaults to { GetDefaultPort() }");
            builder.AppendLine($"#apiport={ GetDefaultPort() }");
            builder.AppendLine($"#Keep Alive interval (set in seconds). Default: 0 (no keep alive)");
            builder.AppendLine($"#keepalive=0");
        }
    }
}
