using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// Configuration related to RPC interface.
    /// </summary>
    public class RpcSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Indicates whether the RPC server is being used</summary>
        public bool Server { get; private set; }

        /// <summary>User name for RPC authorization.</summary>
        public string RpcUser { get; set; }

        /// <summary>Password for RPC authorization.</summary>
        public string RpcPassword { get; set; }

        /// <summary>TCP port for RPC interface.</summary>
        public int RPCPort { get; set; }

        /// <summary>Default bindings from config.</summary>
        public List<IPEndPoint> DefaultBindings { get; set; }

        /// <summary>List of network endpoints that the node will listen and provide RPC on.</summary>
        public List<IPEndPoint> Bind { get; set; }

        /// <summary>List of IP addresses that are allowed to connect to RPC interfaces.</summary>
        public List<IPAddressBlock> AllowIp { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public RpcSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(RpcSettings).FullName);

            this.Bind = new List<IPEndPoint>();
            this.DefaultBindings = new List<IPEndPoint>();
            this.AllowIp = new List<IPAddressBlock>();

            // Get values from config
            this.LoadSettingsFromConfig(nodeSettings);

            // Check validity of settings
            this.CheckConfigurationValidity(nodeSettings.Logger);
        }

        /// <summary>
        /// Loads the rpc settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        private void LoadSettingsFromConfig(NodeSettings nodeSettings)
        {
            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.Server = config.GetOrDefault<bool>("server", false, this.logger);
            this.RPCPort = config.GetOrDefault<int>("rpcport", nodeSettings.Network.RPCPort, this.logger);

            if (this.Server)
            {
                this.RpcUser = config.GetOrDefault<string>("rpcuser", null, this.logger);
                this.RpcPassword = config.GetOrDefault<string>("rpcpassword", null); // No logging!

                try
                {
                    this.AllowIp = config
                        .GetAll("rpcallowip", this.logger)
                        .Select(p => IPAddressBlock.Parse(p))
                        .ToList();
                }
                catch (FormatException)
                {
                    throw new ConfigurationException("Invalid rpcallowip value");
                }

                try
                {
                    this.DefaultBindings = config
                        .GetAll("rpcbind", this.logger)
                        .Select(p => p.ToIPEndPoint(this.RPCPort))
                        .ToList();
                }
                catch (FormatException)
                {
                    throw new ConfigurationException("Invalid rpcbind value");
                }
            }
        }

        /// <summary>
        /// Checks the validity of the RPC settings or forces them to be valid.
        /// </summary>
        /// <param name="logger">Logger to use.</param>
        private void CheckConfigurationValidity(ILogger logger)
        {
            // Check that the settings are valid or force them to be valid
            // (Note that these values will not be set if server = false in the config)
            if (this.RpcPassword == null && this.RpcUser != null)
                throw new ConfigurationException("rpcpassword should be provided");
            if (this.RpcUser == null && this.RpcPassword != null)
                throw new ConfigurationException("rpcuser should be provided");

            // We can now safely assume that server was set to true in the config or that the
            // "AddRpc" callback provided a user and password implying that the Rpc feature will be used.
            if (this.RpcPassword != null && this.RpcUser != null)
            {
                // this.Server = true;

                // If the "Bind" list has not been specified via callback..
                if (this.Bind.Count == 0)
                    this.Bind = this.DefaultBindings;

                if (this.AllowIp.Count == 0)
                {
                    if (this.Bind.Count > 0)
                        logger.LogWarning("WARNING: RPC bind selection (-rpcbind) was ignored because allowed ip's (-rpcallowip) were not specified, refusing to allow everyone to connect");

                    this.Bind.Clear();
                    this.Bind.Add(new IPEndPoint(IPAddress.Parse("::1"), this.RPCPort));
                    this.Bind.Add(new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.RPCPort));
                }

                if (this.Bind.Count == 0)
                {
                    this.Bind.Add(new IPEndPoint(IPAddress.Parse("::"), this.RPCPort));
                    this.Bind.Add(new IPEndPoint(IPAddress.Parse("0.0.0.0"), this.RPCPort));
                }
            }
        }

        /// <summary> Prints the help information on how to configure the rpc settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            NodeSettings defaults = NodeSettings.Default(network);
            var builder = new StringBuilder();

            builder.AppendLine($"-server=<0 or 1>          Accept command line and JSON-RPC commands. Default false.");
            builder.AppendLine($"-rpcuser=<string>         Username for JSON-RPC connections");
            builder.AppendLine($"-rpcpassword=<string>     Password for JSON-RPC connections");
            builder.AppendLine($"-rpcport=<0-65535>        Listen for JSON-RPC connections on <port>. Default: {network.RPCPort}");
            builder.AppendLine($"-rpcbind=<ip:port>        Bind to given address to listen for JSON-RPC connections. This option can be specified multiple times. Default: bind to all interfaces");
            builder.AppendLine($"-rpcallowip=<ip>          Allow JSON-RPC connections from specified source. This option can be specified multiple times.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>Obtains a list of HTTP URLs to RPC interfaces.</summary>
        /// <returns>List of HTTP URLs to RPC interfaces.</returns>
        public string[] GetUrls()
        {
            return this.Bind.Select(b => "http://" + b + "/").ToArray();
        }
    }
}
