using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Networks;

namespace Stratis.FederatedPeg.IntegrationTests.Tools.FederatedNetworkScripts
{
    public class NodeSetup
    {
        public string Name { get; }
        public NodeType NodeType { get; }
        public NetworkType NetworkType { get; }
        public string DaemonPath { get; }
        public List<string> NodesAdded { get; private set; } = new List<string>();
        public List<string> NodesConnetedTo { get; private set; } = new List<string>();
        public string CustomArguments { get; private set; }
        public int? Port { get; private set; }
        public int? ApiPort { get; private set; }
        public int? CounterChainApiPort { get; private set; }
        public string AgentPrefix { get; private set; }
        public string DataDir { get; private set; }
        public List<string> FederationIps { get; private set; } = new List<string>();
        public string RedeemScript { get; private set; }
        public string PublicKey { get; private set; }
        public string ConsoleColor { get; private set; } = "OF";

        private NodeSetup(string name, NodeType nodeType, NetworkType networkType, string daemonPath)
        {
            this.Name = name;
            this.NodeType = nodeType;
            this.NetworkType = networkType;
            this.DaemonPath = daemonPath;
        }

        /// <summary>
        /// Configures the specified node type.
        /// </summary>
        /// <param name="name">Friendly name of the node (this isn't the agent prefix).</param>
        /// <param name="nodeType">Type of the node.</param>
        /// <param name="networkType">Type of the network.</param>
        /// <param name="daemonPath">Path to the node Daemon to run</param>
        /// <param name="defaultConfigurator">Optional function to setup a default NodeSetup configuration</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public static NodeSetup Configure(string name, NodeType nodeType, NetworkType networkType, string daemonPath, Func<NodeSetup, NodeSetup> defaultConfigurator = null)
        {
            NodeSetup nodeSetup = new NodeSetup(name, nodeType, networkType, daemonPath);
            defaultConfigurator?.Invoke(nodeSetup);
            return nodeSetup;
        }

        /// <summary>
        /// Set the console color (e.g. "0D").
        /// </summary>
        /// <param name="color">The color to set.
        /// It's a two Hex digit, first digit = background, second digit = foreground.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup SetConsoleColor(string color)
        {
            this.ConsoleColor = color;
            return this;
        }

        /// <summary>
        /// Adds the specified addresses:ports as -addnode.
        /// </summary>
        /// <param name="nodes">The nodes to add.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup AddNodes(IEnumerable<string> nodes)
        {
            this.NodesAdded.AddRange(nodes);
            return this;
        }

        /// <summary>
        /// Adds the specified addresses:ports as -connect.
        /// </summary>
        /// <param name="nodes">The nodes to connect to.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup ConnectsTo(IEnumerable<string> nodes)
        {
            this.NodesConnetedTo.AddRange(nodes);
            return this;
        }

        /// <summary>
        /// Specifies the port the node listens to.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithPort(string port)
        {
            this.Port = int.Parse(port);
            return this;
        }

        /// <summary>
        /// Specifies the port the node API feature listens to.
        /// </summary>
        /// <param name="apiPort">The API port.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithApiPort(string apiPort)
        {
            this.ApiPort = int.Parse(apiPort);
            return this;
        }

        /// <summary>
        /// Specifies the counter chain api port.
        /// </summary>
        /// <param name="counterChainApiPort">The CounterChain API port.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithCounterChainApiPort(string counterChainApiPort)
        {
            this.CounterChainApiPort = int.Parse(counterChainApiPort);
            return this;
        }

        /// <summary>
        /// Specifies the node agent prefix.
        /// </summary>
        /// <param name="agentPrefix">The Agent prefix.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithAgentPrefix(string agentPrefix)
        {
            this.AgentPrefix = agentPrefix;
            return this;
        }

        /// <summary>
        /// Specifies the DataDir argument.
        /// </summary>
        /// <param name="dataDir">The DataDir argument</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithDataDir(string dataDir)
        {
            this.DataDir = dataDir;
            return this;
        }

        /// <summary>
        /// Setup the specified FederationIP argument.
        /// </summary>
        /// <param name="federationIps">The federation members IP addresses.</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithFederationIps(IEnumerable<string> federationIps)
        {
            this.FederationIps.AddRange(federationIps);
            return this;
        }

        /// <summary>
        /// Specifies the RedeemScript argument.
        /// </summary>
        /// <param name="redeemScript">The Redeem Script</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithRedeemScript(string redeemScript)
        {
            this.RedeemScript = redeemScript;
            return this;
        }

        /// <summary>
        /// Specifies the PublicKey argument.
        /// </summary>
        /// <param name="publicKey">The Public Key</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithPublicKey(string publicKey)
        {
            this.PublicKey = publicKey;
            return this;
        }

        /// <summary>
        /// Customs arguments to apply to this node setup
        /// </summary>
        /// <param name="arguments">The arguments to apply to this node setup</param>
        /// <returns><see cref="NodeSetup"/> instance</returns>
        public NodeSetup WithCustomArguments(string arguments)
        {
            this.CustomArguments = arguments;
            return this;
        }

        /// <summary>
        /// Generates the command arguments to use when starting up the node.
        /// </summary>
        /// <returns>The command arguments to use when starting up the node</returns>
        public string GenerateCommandArguments()
        {
            StringBuilder sb = new StringBuilder(this.GenerateNetworkTypeArg());

            if (!string.IsNullOrWhiteSpace(this.AgentPrefix))
                sb.Append($"-agentprefix={this.AgentPrefix} ");

            if (!string.IsNullOrWhiteSpace(this.DataDir))
                sb.Append($"-datadir={this.DataDir} ");

            if (this.Port != null)
                sb.Append($"-port={this.Port} ");

            if (this.ApiPort != null)
                sb.Append($"-apiport={this.ApiPort} ");

            if (this.CounterChainApiPort != null)
                sb.Append($"-counterchainapiport={this.CounterChainApiPort} ");

            if (this.FederationIps.Count > 0)
                sb.Append($"-federationips={string.Join(",", this.FederationIps)} ");

            if (!string.IsNullOrWhiteSpace(this.RedeemScript))
                sb.Append($"-redeemscript=\"\"{this.RedeemScript}\"\" ");

            if (!string.IsNullOrWhiteSpace(this.PublicKey))
                sb.Append($"-publickey={this.PublicKey} ");

            if (this.NodesAdded.Count > 0)
                sb.Append(string.Join("", this.NodesAdded.Select(address => $"-addnode={address} ")));

            if (this.NodesConnetedTo.Count > 0)
                sb.Append(string.Join("", this.NodesConnetedTo.Select(address => $"-connect={address} ")));

            sb.Append(this.CustomArguments ?? string.Empty);

            return sb.ToString();
        }

        private string GenerateNetworkTypeArg()
        {
            string value;
            switch (this.NodeType)
            {
                case NodeType.GatewayMain:
                    value = "-mainchain ";
                    break;
                case NodeType.GatewaySide:
                    value = "-sidechain ";
                    break;
                default:
                    value = String.Empty;
                    break;
            }

            switch (this.NetworkType)
            {
                case NetworkType.Main:
                    return value;
                case NetworkType.Testnet:
                    return value + "-testnet ";
                case NetworkType.Regtest:
                    return value + "-regtest ";
                default:
                    throw new ArgumentException("Unknown Network Type");
            }
        }
    }
}
