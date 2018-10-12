using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Responsible for managing the DNS feature.
    /// </summary>
    public class DnsFeature : FullNodeFeature
    {
        /// <summary>
        /// Defines a back-off interval in milliseconds if the DNS server fails whilst operating, before it restarts.
        /// </summary>
        public const int DnsServerBackoffInterval = 2000;

        /// <summary>
        /// Defines the name of the masterfile on disk.
        /// </summary>
        public const string DnsMasterFileName = "masterfile.json";

        /// <summary>
        /// Defines the DNS server.
        /// </summary>
        private readonly IDnsServer dnsServer;

        /// <summary>
        /// Defines the whitelist manager.
        /// </summary>
        private readonly IWhitelistManager whitelistManager;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Defines the node lifetime.
        /// </summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// Defines the node settings for the node.
        /// </summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>
        /// Defines the DNS settings for the node.
        /// </summary>
        private readonly DnsSettings dnsSettings;

        /// <summary>
        /// Defines the data folders of the system.
        /// </summary>
        private readonly DataFolder dataFolders;

        /// <summary>
        /// Defines the long running task used to support the DNS service.
        /// </summary>
        private Task dnsTask;

        /// <summary>
        /// Factory for creating background async loop tasks.
        /// </summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>Manager of node's network connections.</summary>
        private readonly IConnectionManager connectionManager;


        /// <summary>
        /// Instance of the UnreliablePeerBehavior to the connectionManager Template.
        /// </summary>
        private readonly UnreliablePeerBehavior unreliablePeerBehavior;

        /// <summary>
        /// The async loop to refresh the whitelist.
        /// </summary>
        private IAsyncLoop whitelistRefreshLoop;

        /// <summary>
        /// Defines a flag used to indicate whether the object has been disposed or not.
        /// </summary>
        private bool disposed = false;


        /// <summary>
        /// Initializes a new instance of the <see cref="DnsFeature"/> class.
        /// </summary>
        /// <param name="dnsServer">The DNS server.</param>
        /// <param name="whitelistManager">The whitelist manager.</param>
        /// <param name="loggerFactory">The factory to create the logger.</param>
        /// <param name="nodeLifetime">The node lifetime object used for graceful shutdown.</param>
        /// <param name="nodeSettings">The node settings object containing node configuration.</param>
        /// <param name="dnsSettings">Defines the DNS settings for the node</param>
        /// <param name="dataFolders">The data folders of the system.</param>
        /// <param name="asyncLoopFactory">The asynchronous loop factory.</param>
        /// <param name="connectionManager">Manager of node's network connections.</param>
        /// <param name="unreliablePeerBehavior">Instance of the UnreliablePeerBehavior that will be added to the connectionManager Template.</param>
        public DnsFeature(IDnsServer dnsServer, IWhitelistManager whitelistManager, ILoggerFactory loggerFactory, INodeLifetime nodeLifetime, DnsSettings dnsSettings, NodeSettings nodeSettings, DataFolder dataFolders, IAsyncLoopFactory asyncLoopFactory, IConnectionManager connectionManager, UnreliablePeerBehavior unreliablePeerBehavior)
        {
            Guard.NotNull(dnsServer, nameof(dnsServer));
            Guard.NotNull(whitelistManager, nameof(whitelistManager));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(dataFolders, nameof(dataFolders));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));
            Guard.NotNull(connectionManager, nameof(connectionManager));
            Guard.NotNull(unreliablePeerBehavior, nameof(unreliablePeerBehavior));

            this.dnsServer = dnsServer;
            this.whitelistManager = whitelistManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.asyncLoopFactory = asyncLoopFactory;
            this.nodeLifetime = nodeLifetime;
            this.nodeSettings = nodeSettings;
            this.dnsSettings = dnsSettings;
            this.dataFolders = dataFolders;
            this.connectionManager = connectionManager;
            this.unreliablePeerBehavior = unreliablePeerBehavior;
        }

        /// <summary>
        /// Initializes the DNS feature.
        /// </summary>
        public override Task InitializeAsync()
        {
            // Create long running task for DNS service.
            this.dnsTask = Task.Factory.StartNew(this.RunDnsService, TaskCreationOptions.LongRunning);

            this.StartWhitelistRefreshLoop();

            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;
            connectionParameters.TemplateBehaviors.Add(this.unreliablePeerBehavior);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs the DNS service until the node is stopped.
        /// </summary>
        private void RunDnsService()
        {
            // Initialize DNS server.
            this.dnsServer.Initialize();

            while (true)
            {
                try
                {
                    this.logger.LogInformation("Starting DNS server on port {0}", this.dnsSettings.DnsListenPort);

                    // Start.
                    this.dnsServer.ListenAsync(this.dnsSettings.DnsListenPort, this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Node shutting down, expected.
                    this.logger.LogInformation("Stopping DNS");
                    break;
                }
                catch (Exception e)
                {
                    this.logger.LogError(e, "Failed whilst running the DNS server with: {0}", e.Message);

                    try
                    {
                        // Back-off before restart.
                        Task.Delay(DnsServerBackoffInterval, this.nodeLifetime.ApplicationStopping).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        // Node shutting down, expected.
                        this.logger.LogInformation("Stopping DNS");
                        break;
                    }

                    this.logger.LogTrace("Restarting DNS server following previous failure.");
                }
            }
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            DnsSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            DnsSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <summary>
        /// Disposes of the object.
        /// </summary>
        public override void Dispose()
        {
            this.logger.LogInformation("Stopping DNS...");

            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the object.
        /// </summary>
        /// <param name="disposing"><c>true</c> if the object is being disposed of deterministically, otherwise <c>false</c>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    IDisposable disposablewhitelistRefreshLoop = this.whitelistRefreshLoop;
                    disposablewhitelistRefreshLoop?.Dispose();
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Starts the loop to refresh the whitelist.
        /// </summary>
        private void StartWhitelistRefreshLoop()
        {
            this.whitelistRefreshLoop = this.asyncLoopFactory.Run($"{nameof(DnsFeature)}.WhitelistRefreshLoop", token =>
            {
                this.whitelistManager.RefreshWhitelist();
                return Task.CompletedTask;
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromSeconds(30));
        }
    }
}