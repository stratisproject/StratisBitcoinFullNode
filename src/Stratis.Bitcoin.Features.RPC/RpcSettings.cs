using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.RPC
{
    /// <summary>
    /// Configuration related to RPC interface.
    /// </summary>
    public class RpcSettings
    {
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
        public List<IPAddress> AllowIp { get; set; }

        private Action<RpcSettings> callback = null;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public RpcSettings()
        {
            this.Bind = new List<IPEndPoint>();
            this.DefaultBindings = new List<IPEndPoint>();
            this.AllowIp = new List<IPAddress>();
        }

        public RpcSettings(Action<RpcSettings> callback)
            :this()
        {
            this.callback = callback;
        }
        
        /// <summary>
        /// Loads the rpc settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        private void LoadSettingsFromConfig(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            this.Server = config.GetOrDefault<bool>("server", false);
            this.RPCPort = nodeSettings.Network.RPCPort;

            if (this.Server)
            {
                this.RpcUser = config.GetOrDefault<string>("rpcuser", null);
                this.RpcPassword = config.GetOrDefault<string>("rpcpassword", null);
                this.RPCPort = config.GetOrDefault<int>("rpcport", nodeSettings.Network.RPCPort);

                try
                {
                    this.AllowIp = config
                        .GetAll("rpcallowip")
                        .Select(p => IPAddress.Parse(p))
                        .ToList();
                }
                catch (FormatException)
                {
                    throw new ConfigurationException("Invalid rpcallowip value");
                }

                try
                {
                    this.DefaultBindings = config
                        .GetAll("rpcbind")
                        .Select(p => NodeSettings.ConvertToEndpoint(p, this.RPCPort))
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
            this.Server = true;

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

        /// <summary>
        /// Loads the rpc settings from the application configuration.
        /// Allows the callback to override those settings.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            // Get values from config
            this.LoadSettingsFromConfig(nodeSettings);

            // Invoke callback
            this.callback?.Invoke(this);

            // Check validity of settings
            this.CheckConfigurationValidity(nodeSettings.Logger);
        }

        public static void PrintHelp(Network mainNet)
        {
            var defaults = NodeSettings.Default();
            var builder = new StringBuilder();

            builder.AppendLine($"-server=<0 or 1>          Accept command line and JSON-RPC commands. Default false.");
            builder.AppendLine($"-rpcuser=<string>         Username for JSON-RPC connections");
            builder.AppendLine($"-rpcpassword=<string>     Password for JSON-RPC connections");
            builder.AppendLine($"-rpcport=<0-65535>        Listen for JSON-RPC connections on <port>. Default: {mainNet.RPCPort} or (reg)testnet: {Network.TestNet.RPCPort}");
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