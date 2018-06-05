using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.RPC;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp
{
    public enum CoreNodeState
    {
        Stopped,
        Starting,
        Running,
        Killed
    }

    public class CoreNode
    {
        /// <summary>Factory for creating P2P network peers.</summary>
        private readonly INetworkPeerFactory networkPeerFactory;

        private int[] ports;
        private INodeRunner runner;
        private object lockObject = new object();
        private readonly NetworkCredential creds;

        public string Folder { get; }

        /// <summary>Location of the data directory for the node.</summary>
        public string DataFolder { get; }

        public IPEndPoint Endpoint { get { return new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ports[0]); } }

        public string Config { get; }

        public NodeConfigParameters ConfigParameters { get; } = new NodeConfigParameters();

        public CoreNodeState State { get; private set; }

        private Network network;

        public CoreNode(string folder, INodeRunner runner, NodeBuilder builder, Network network,
            bool cleanfolders = true, string configfile = "stratis.conf")
        {
            this.network = network;
            this.runner = runner;
            this.Folder = folder;
            this.State = CoreNodeState.Stopped;
            if (cleanfolders)
                this.CleanFolder();

            Directory.CreateDirectory(folder);
            var pass = Encoders.Hex.EncodeData(RandomUtils.GetBytes(20));
            this.creds = new NetworkCredential(pass, pass);
            this.DataFolder = Path.Combine(folder, "data");
            Directory.CreateDirectory(this.DataFolder);

            this.Config = Path.Combine(this.DataFolder, configfile);
            this.ConfigParameters.Import(builder.ConfigParameters);
            this.ports = new int[3];
            this.FindPorts(this.ports);

            var loggerFactory = new ExtendedLoggerFactory();
            this.AddConsoleWithFilters(loggerFactory);

            this.networkPeerFactory = new NetworkPeerFactory(network, DateTimeProvider.Default, loggerFactory, new PayloadProvider().DiscoverPayloads(), new SelfEndpointTracker());
        }

        private void AddConsoleWithFilters(ILoggerFactory loggerFactory)
        {
            ConsoleLoggerSettings consoleLoggerSettings = new ConsoleLoggerSettings
            {
                Switches =
                {
                    {"Default", Microsoft.Extensions.Logging.LogLevel.Information},
                    {"System", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning},
                    {"Microsoft.AspNetCore", Microsoft.Extensions.Logging.LogLevel.Error},
                    {"Stratis", Microsoft.Extensions.Logging.LogLevel.Trace}
                }
            };

            ConsoleLoggerProvider consoleLoggerProvider = new ConsoleLoggerProvider(consoleLoggerSettings);
            loggerFactory.AddProvider(consoleLoggerProvider);

            ExtendedLoggerFactory extendedLoggerFactory = loggerFactory as ExtendedLoggerFactory;
            Guard.NotNull(extendedLoggerFactory, nameof(extendedLoggerFactory));
            extendedLoggerFactory.ConsoleLoggerProvider = consoleLoggerProvider;
            extendedLoggerFactory.ConsoleSettings = consoleLoggerSettings;
        }

        public FullNode FullNode {
            get {
                    return ((StratisBitcoinPosRunner)this.runner).FullNode;
            }
        }

        public RPCClient CreateRPCClient()
        {
            return new RPCClient(this.creds, new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"), this.network);
        }

        public RestClient CreateRESTClient()
        {
            return new RestClient(new Uri("http://127.0.0.1:" + this.ports[1].ToString() + "/"));
        }

        public INetworkPeer CreateNetworkPeerClient()
        {
            return this.networkPeerFactory.CreateConnectedNetworkPeerAsync("127.0.0.1:" + this.ports[0].ToString()).GetAwaiter().GetResult();
        }

        private void CleanFolder()
        {
            TestUtils.ShellCleanupFolder(this.Folder);
        }

        public int ProtocolPort {
            get { return this.ports[0]; }
        }

        public int ApiPort{
            get { return this.ports[2]; }
        }

        public void Start()
        {
            NodeConfigParameters config = new NodeConfigParameters();
            config.Add("regtest", "1");
            config.Add("rest", "1");
            config.Add("server", "1");
            config.Add("txindex", "1");
            config.Add("port", this.ports[0].ToString());
            config.Add("rpcport", this.ports[1].ToString());
            config.Add("rpcuser", this.creds.UserName);
            config.Add("rpcpassword", this.creds.Password);
            config.Add("printtoconsole", "1");
            config.Add("keypool", "10");
            config.Add("apiport", this.ports[2].ToString());
            config.Import(this.ConfigParameters);
            File.WriteAllText(this.Config, config.ToString());
            lock (this.lockObject)
            {
                this.runner.Start(this.network, this.DataFolder);
                this.State = CoreNodeState.Starting;
            }
            while (true)
            {
                try
                {
                    //await this.CreateRPCClient().GetBlockHashAsync(0);
                    this.State = CoreNodeState.Running;
                    break;
                }
                catch
                {
                }
                if (this.runner.IsDisposed)
                    break;
            }
        }

        private void FindPorts(int[] ports)
        {
            int i = 0;
            while (i < ports.Length)
            {
                var port = RandomUtils.GetUInt32() % 4000;
                port = port + 10000;
                if (ports.Any(p => p == port))
                    continue;
                try
                {
                    TcpListener l = new TcpListener(IPAddress.Loopback, (int)port);
                    l.Start();
                    l.Stop();
                    ports[i] = (int)port;
                    i++;
                }
                catch (SocketException)
                {
                }
            }
        }

        public void Kill()
        {
            lock (this.lockObject)
            {
                this.runner.Kill();
                this.State = CoreNodeState.Killed;
            }
        }
    }
}
