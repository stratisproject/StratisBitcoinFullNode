using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Responsible for managing the Dns feature.
    /// </summary>
    public class DnsFeature : FullNodeFeature
    {
        /// <summary>
        /// Defines the name of the masterfile on disk.
        /// </summary>
        private const string DnsMasterFileName = "masterfile.dat";

        /// <summary>
        /// Defines the DNS server.
        /// </summary>
        private readonly IDnsServer dnsServer;

        /// <summary>
        /// Defines the DNS masterfile.
        /// </summary>
        private readonly IMasterFile masterFile;

        /// <summary>
        /// Defines the peer address manager.
        /// </summary>
        private readonly IPeerAddressManager peerAddressManager;

        /// <summary>
        /// Defines the logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Defines the node lifetime.
        /// </summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>
        /// Defines the configuration settings for the node.
        /// </summary>
        private readonly NodeSettings nodeSettings;

        /// <summary>
        /// Defines the data folders of the system.
        /// </summary>
        private readonly DataFolder dataFolders;

        /// <summary>
        /// Defines the long running task used to support the DNS service.
        /// </summary>
        private Task dnsTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsFeature"/> class.
        /// </summary>
        /// <param name="dnsServer">The DNS server.</param>
        /// <param name="masterFile">The DNS masterfile.</param>
        /// <param name="peerAddressManager">The peer address manager.</param>
        /// <param name="loggerFactory">The factory to create the logger.</param>
        /// <param name="nodeLifetime">The node lifetime object used for graceful shutdown.</param>
        /// <param name="nodeSettings">The node settings object containing node configuration.</param>
        /// <param name="dataFolders">The data folders of the system.</param>
        public DnsFeature(IDnsServer dnsServer, IMasterFile masterFile, IPeerAddressManager peerAddressManager, ILoggerFactory loggerFactory, INodeLifetime nodeLifetime, NodeSettings nodeSettings, DataFolder dataFolders)
        {
            Guard.NotNull(dnsServer, nameof(dnsServer));
            Guard.NotNull(masterFile, nameof(masterFile));
            Guard.NotNull(peerAddressManager, nameof(peerAddressManager));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            Guard.NotNull(dataFolders, nameof(dataFolders));

            this.dnsServer = dnsServer;
            this.masterFile = masterFile;
            this.peerAddressManager = peerAddressManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeLifetime = nodeLifetime;
            this.nodeSettings = nodeSettings;
            this.dataFolders = dataFolders;
        }

        /// <summary>
        /// Initializes the Dns feature.
        /// </summary>
        public override void Initialize()
        {
            this.logger.LogTrace("()");

            // Create long running task for DNS service
            this.dnsTask = Task.Factory.StartNew(this.RunDnsServiceAsync, TaskCreationOptions.LongRunning);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Runs the DNS service until the node is stopped.
        /// </summary>
        /// <returns>A task used to allow the caller to await the operation.</returns>
        private async Task RunDnsServiceAsync()
        {
            this.logger.LogTrace("()");

            try
            {
                // Load masterfile from disk if it exists
                string path = Path.Combine(this.dataFolders.DnsMasterFilePath + DnsMasterFileName);
                if (File.Exists(path))
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        this.masterFile.Load(stream);
                    }

                    // Swap in masterfile from disk into DNS server
                    this.dnsServer.SwapMasterfile(this.masterFile);
                }

                this.logger.LogInformation("Starting DNS server on port {0}", this.nodeSettings.DnsListenPort);

                // Start
                await this.dnsServer.ListenAsync(this.nodeSettings.DnsListenPort, this.nodeLifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // Node shutting down, expected
                this.logger.LogInformation("Stopping DNS");
            }

            this.logger.LogTrace("(-)");
        }
    }
}
