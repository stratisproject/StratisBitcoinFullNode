using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    /// <summary>
    /// Node providing all supported features of the blockchain and its network.
    /// </summary>
    public class FullNode : IFullNode
    {
        /// <summary>Instance logger.</summary>
        private ILogger logger;

        /// <summary>Component responsible for starting and stopping all the node's features.</summary>
        private FullNodeFeatureExecutor fullNodeFeatureExecutor;

        /// <summary>Indicates whether the node has been stopped or is currently being stopped.</summary>
        internal bool Stopped;

        /// <summary>Indicates whether the node's instance has been disposed or is currently being disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>Indicates whether the node's instance disposal has been finished.</summary>
        public bool HasExited { get; private set; }

        /// <summary>Node command line and configuration file settings.</summary>
        public NodeSettings Settings { get; private set; }

        /// <summary>List of disposable resources that the node uses.</summary>
        public List<IDisposable> Resources { get; private set; }

        /// <summary>Information about the best chain.</summary>
        public ChainState ChainBehaviorState { get; private set; }

        /// <summary>Provider of notification about newly available blocks and transactions.</summary>
        public Signals.Signals Signals { get; set; }

        /// <summary>ASP.NET Core host for RPC server.</summary>
        public IWebHost RPCHost { get; set; }

        /// <summary>Component responsible for connections to peers in P2P network.</summary>
        public IConnectionManager ConnectionManager { get; set; }

        /// <summary>Best chain of block headers from genesis.</summary>
        public ConcurrentChain Chain { get; set; }

        /// <summary>Factory for creating and execution of asynchronous loops.</summary>
        public IAsyncLoopFactory AsyncLoopFactory { get; set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        public Network Network { get; internal set; }

        /// <summary>Contains path locations to folders and files on disk.</summary>
        public DataFolder DataFolder { get; set; }

        /// <summary>Provider of date time functionality.</summary>
        public IDateTimeProvider DateTimeProvider { get; set; }

        /// <summary>Application life cycle control - triggers when application shuts down.</summary>
        private NodeLifetime nodeLifetime;

        /// <inheritdoc />
        public INodeLifetime NodeLifetime
        {
            get { return this.nodeLifetime; }
            private set { this.nodeLifetime = (NodeLifetime)value; }
        }

        /// <inheritdoc />
        public IFullNodeServiceProvider Services { get; set; }

        public T NodeService<T>(bool failWithDefault = false)
        {
            if (this.Services != null && this.Services.ServiceProvider != null)
            {
                var service = this.Services.ServiceProvider.GetService<T>();
                if (service != null)
                    return service;
            }

            if (failWithDefault)
                return default(T);

            throw new InvalidOperationException($"The {typeof(T).ToString()} service is not supported");
        }

        public T NodeFeature<T>(bool failWithError = false)
        {
            if (this.Services != null)
            {
                var feature = this.Services.Features.OfType<T>().FirstOrDefault();
                if (feature != null)
                    return feature;
            }

            if (!failWithError)
                return default(T);

            throw new InvalidOperationException($"The {typeof(T).ToString()} feature is not supported");
        }

        /// <inheritdoc />
        public Version Version
        {
            get
            {
                string versionString = typeof(FullNode).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ??
                    Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;

                if (!string.IsNullOrEmpty(versionString))
                {
                    try
                    {
                        return new Version(versionString);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (OverflowException)
                    {
                    }
                }

                return new Version(0, 0);
            }
        }

        /// <summary>
        /// Initializes DI services that the node needs.
        /// </summary>
        /// <param name="serviceProvider">Provider of DI services.</param>
        /// <returns>Full node itself to allow fluent code.</returns>
        public FullNode Initialize(IFullNodeServiceProvider serviceProvider)
        {
            Guard.NotNull(serviceProvider, nameof(serviceProvider));

            this.Services = serviceProvider;

            this.logger = this.Services.ServiceProvider.GetService<ILoggerFactory>().CreateLogger(this.GetType().FullName);

            this.DataFolder = this.Services.ServiceProvider.GetService<DataFolder>();
            this.DateTimeProvider = this.Services.ServiceProvider.GetService<IDateTimeProvider>();
            this.Network = this.Services.ServiceProvider.GetService<Network>();
            this.Settings = this.Services.ServiceProvider.GetService<NodeSettings>();
            this.ChainBehaviorState = this.Services.ServiceProvider.GetService<ChainState>();
            this.Chain = this.Services.ServiceProvider.GetService<ConcurrentChain>();
            this.Signals = this.Services.ServiceProvider.GetService<Signals.Signals>();

            this.ConnectionManager = this.Services.ServiceProvider.GetService<IConnectionManager>();
            this.AsyncLoopFactory = this.Services.ServiceProvider.GetService<IAsyncLoopFactory>();

            this.logger.LogInformation($"Full node initialized on {this.Network.Name}");

            return this;
        }

        /// <inheritdoc />
        public void Start()
        {
            if (this.IsDisposed)
                throw new ObjectDisposedException(nameof(FullNode));

            if (this.Resources != null)
                throw new InvalidOperationException("node has already started.");

            this.Resources = new List<IDisposable>();
            this.nodeLifetime = this.Services.ServiceProvider.GetRequiredService<INodeLifetime>() as NodeLifetime;
            this.fullNodeFeatureExecutor = this.Services.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

            if (this.nodeLifetime == null)
                throw new InvalidOperationException($"{nameof(INodeLifetime)} must be set.");

            if (this.fullNodeFeatureExecutor == null)
                throw new InvalidOperationException($"{nameof(FullNodeFeatureExecutor)} must be set.");

            this.logger.LogInformation("Starting node...");

            // Start all registered features.
            this.fullNodeFeatureExecutor.Start();

            // Start connecting to peers.
            this.ConnectionManager.Start();

            // Fire INodeLifetime.Started.
            this.nodeLifetime.NotifyStarted();

            this.StartPeriodicLog();
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (this.Stopped)
                return;

            this.Stopped = true;

            this.logger.LogInformation("Closing node pending...");

            // Fire INodeLifetime.Stopping.
            this.nodeLifetime.StopApplication();

            this.ConnectionManager.Dispose();

            foreach (IDisposable dispo in this.Resources)
                dispo.Dispose();

            // Fire the NodeFeatureExecutor.Stop.
            this.fullNodeFeatureExecutor.Stop();
            (this.Services.ServiceProvider as IDisposable)?.Dispose();

            // Fire INodeLifetime.Stopped.
            this.nodeLifetime.NotifyStopped();
        }

        /// <summary>
        /// Starts a loop to periodically log statistics about node's status very couple of seconds.
        /// <para>
        /// These logs are also displayed on the console.
        /// </para>
        /// </summary>
        private void StartPeriodicLog()
        {
            IAsyncLoop periodicLogLoop = this.AsyncLoopFactory.Run("PeriodicLog", (cancellation) =>
            {
                StringBuilder benchLogs = new StringBuilder();

                benchLogs.AppendLine("======Node stats====== " + this.DateTimeProvider.GetUtcNow().ToString(CultureInfo.InvariantCulture) + " agent " +
                                     this.ConnectionManager.Parameters.UserAgent);

                // Display node stats grouped together.
                foreach (var feature in this.Services.Features.OfType<INodeStats>())
                    feature.AddNodeStats(benchLogs);

                // Now display the other stats.
                foreach (var feature in this.Services.Features.OfType<IFeatureStats>())
                    feature.AddFeatureStats(benchLogs);

                benchLogs.AppendLine();
                benchLogs.AppendLine("======Connection======");
                benchLogs.AppendLine(this.ConnectionManager.GetNodeStats());
                this.logger.LogInformation(benchLogs.ToString());
                return Task.CompletedTask;
            },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.FiveSeconds,
                startAfter: TimeSpans.FiveSeconds);

            this.Resources.Add(periodicLogLoop);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.IsDisposed)
                return;

            this.IsDisposed = true;

            if (!this.Stopped)
            {
                try
                {
                    this.Stop();
                }
                catch (Exception ex)
                {
                    this.logger?.LogError(ex.Message);
                }
            }

            this.HasExited = true;
        }
    }
}
