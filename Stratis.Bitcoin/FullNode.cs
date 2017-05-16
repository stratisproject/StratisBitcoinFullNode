using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Miner;
using Stratis.Bitcoin.Utilities;
using System.Reflection;
using System.Runtime.Loader;

namespace Stratis.Bitcoin
{

	public class FullNode : IFullNode
	{
		private ApplicationLifetime _applicationLifetime; // this will replace the cancellation token on the full node
		private readonly ILogger _logger;
		private FullNodeFeatureExecutor _fullNodeFeatureExecutor;
	    private bool _stopped;
	    private readonly ManualResetEvent _isDisposed = new ManualResetEvent(false);
	    private readonly ManualResetEvent _isStarted = new ManualResetEvent(false);

        public IFullNodeServiceProvider Services { get; set; }

		NodeSettings _settings;

		public NodeSettings Settings
		{
			get { return this._settings; }
		}

		public Version Version
		{
			get
			{
				string versionString = typeof(FullNode).GetTypeInfo().Assembly.GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>()?.Version ??
									   Microsoft.Extensions.PlatformAbstractions.PlatformServices.Default.Application.ApplicationVersion;
				if (!string.IsNullOrEmpty(versionString))
				{
					try
					{
						return new Version(versionString);
					}
					catch (ArgumentException) { }
					catch (OverflowException) { }
				}
				return new Version(0, 0);
			}
		}

		public FullNode()
		{
            this._logger = Logs.LoggerFactory.CreateLogger<FullNode>();
		}

		public FullNode Initialize(IFullNodeServiceProvider serviceProvider)
		{
			Guard.NotNull(serviceProvider, nameof(serviceProvider));

			this.Services = serviceProvider;

			this.DataFolder = this.Services.ServiceProvider.GetService<DataFolder>();
			this.DateTimeProvider = this.Services.ServiceProvider.GetService<IDateTimeProvider>();
			this.Network = this.Services.ServiceProvider.GetService<Network>();
			this._settings = this.Services.ServiceProvider.GetService<NodeSettings>();
			this._ChainBehaviorState = this.Services.ServiceProvider.GetService<BlockStore.ChainBehavior.ChainState>();
			this.CoinView = this.Services.ServiceProvider.GetService<CoinView>();
			this.Chain = this.Services.ServiceProvider.GetService<ConcurrentChain>();
			this.GlobalCancellation = this.Services.ServiceProvider.GetService<CancellationProvider>();
			this.MempoolManager = this.Services.ServiceProvider.GetService<MempoolManager>();
			this.Signals = this.Services.ServiceProvider.GetService<Signals>();

			this.ConnectionManager = this.Services.ServiceProvider.GetService<ConnectionManager>();
			this.BlockStoreManager = this.Services.ServiceProvider.GetService<BlockStoreManager>();
			this.ConsensusLoop = this.Services.ServiceProvider.GetService<ConsensusLoop>();
			this.Miner = this.Services.ServiceProvider.GetService<Mining>();

			_logger.LogDebug("Full node initialized on {0}", Network.Name);

			return this;
		}

		protected void StartFeatures()
		{
			this._applicationLifetime = this.Services?.ServiceProvider.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
			this._fullNodeFeatureExecutor = this.Services?.ServiceProvider.GetRequiredService<FullNodeFeatureExecutor>();

			// Fire IApplicationLifetime.Started
			this._applicationLifetime?.NotifyStarted();

			//start all registered features
			this._fullNodeFeatureExecutor?.Start();
		}

		protected void DisposeFeatures()
		{
			// Fire IApplicationLifetime.Stopping
			this._applicationLifetime?.StopApplication();
			// Fire the IHostedService.Stop
			this._fullNodeFeatureExecutor?.Stop();
			(this.Services.ServiceProvider as IDisposable)?.Dispose();
			//(this.Services.ServiceProvider as IDisposable)?.Dispose();
		}

		public Network Network
		{
			get;
			internal set;
		}

		public CoinView CoinView
		{
			get; set;
		}

		public DataFolder DataFolder
		{
			get; set;
		}

		public IDateTimeProvider DateTimeProvider
		{
			get; set;
		}

		public bool IsInitialBlockDownload()
		{
			//if (fImporting || fReindex)
			//	return true;
			if (this.ConsensusLoop.Tip == null)
				return true;
			if (this.ConsensusLoop.Tip.ChainWork < (this.Network.Consensus.MinimumChainWork ?? uint256.Zero))
				return true;
			if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.Settings.MaxTipAge))
				return true;
			return false;
		}

		List<IDisposable> _Resources = new List<IDisposable>();
		public List<IDisposable> Resources => _Resources;

		public void Start()
		{
			if (IsDisposed)
				throw new ObjectDisposedException("FullNode");
			_isStarted.Reset();

			// start all the features defined
			this.StartFeatures();

			ConnectionManager.Start();
			_isStarted.Set();

			this.StartPeriodicLog();
		}

	    public void Run()
	    {
	        RunAsync().GetAwaiter().GetResult();
	    }

        public async Task RunAsync()
	    {
	        var done = new ManualResetEventSlim(false);
	        using (var cts = new CancellationTokenSource())
	        {
	            Action shutdown = () =>
	            {
	                if (!cts.IsCancellationRequested)
	                {
	                    Console.WriteLine("Application is shutting down...");
	                    try
	                    {
	                        cts.Cancel();
	                    }
	                    catch (ObjectDisposedException)
	                    {
	                    }
	                }

	                done.Wait();
	            };

                var assemblyLoadContext = AssemblyLoadContext.GetLoadContext(typeof(FullNode).GetTypeInfo().Assembly);
                assemblyLoadContext.Unloading += context => shutdown();
                Console.CancelKeyPress += (sender, eventArgs) =>
	            {
	                shutdown();
	                // Don't terminate the process immediately, wait for the Main thread to exit gracefully.
	                eventArgs.Cancel = true;
	            };

	            await this.RunAsync(cts.Token, "Application started. Press Ctrl+C to shut down.");
	            done.Set();
	        }
        }

	    public async Task RunAsync(CancellationToken cancellationToken, string shutdownMessage)
	    {
	        using (this)
	        {
	            await this.StartAsync(cancellationToken);

	            if (!string.IsNullOrEmpty(shutdownMessage))
	            {
	                Console.WriteLine(shutdownMessage);
	            }

                cancellationToken.Register(state =>
                    {
                        ((IApplicationLifetime)state).StopApplication();
                    },
                    this._applicationLifetime);

                var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                this._applicationLifetime.ApplicationStopping.Register(obj =>
                {
                    var tcs = (TaskCompletionSource<object>)obj;
                    tcs.TrySetResult(null);
                }, waitForStop);

                await waitForStop.Task;

	            await StopAsync();
	        }
	    }

	    public async Task StartAsync(CancellationToken cancellationToken)
	    {
	        await Task.Run(() => Start(), cancellationToken);
	    }

	    public async Task StopAsync()
	    {
	        await Task.Run(() => Stop(CancellationToken.None));
	    }

        public void Stop(CancellationToken cancellationToken)
	    {
	        if (this._stopped)
	        {
	            return;
	        }
            this._stopped = true;

            // Fire IApplicationLifetime.Stopping
            this._applicationLifetime?.StopApplication();

	        if (this.GlobalCancellation != null)
	        {
                this.GlobalCancellation.Cancellation.Cancel();

                this.ConnectionManager.Dispose();
	            foreach (var dispo in this._Resources)
	                dispo.Dispose();

	            DisposeFeatures();
	        }

            // Fire IApplicationLifetime.Stopped
            this._applicationLifetime?.NotifyStopped();
	    }

        private BlockStore.ChainBehavior.ChainState _ChainBehaviorState;
		public BlockStore.ChainBehavior.ChainState ChainBehaviorState
		{
			get { return _ChainBehaviorState; }
		}

		public Mining Miner
		{
			get; set;
		}

		public Signals Signals
		{
			get; set;
		}

		public ConsensusLoop ConsensusLoop
		{
			get; set;
		}

		public IWebHost RPCHost
		{
			get; set;
		}

		public ConnectionManager ConnectionManager
		{
			get; set;
		}

		public MempoolManager MempoolManager
		{
			get; set;
		}

		public ChainRepository ChainRepository
		{
			get; set;
		}

		public BlockStoreManager BlockStoreManager
		{
			get; set;
		}

		/// <summary>
		/// The longest PoW chain
		/// </summary>
		public ConcurrentChain Chain
		{
			get; set;
		}

		public PeriodicTask FlushAddrmanTask
		{
			get; set;
		}

		public PeriodicTask FlushChainTask
		{
			get; set;
		}

		public CancellationProvider GlobalCancellation
		{
			get; set;
		}
		public class CancellationProvider
		{
			public CancellationTokenSource Cancellation { get; set; }
		}

		public bool IsDisposed
		{
			get
			{
				return this._isDisposedValue;
			}
		}

		private void StartPeriodicLog()
		{
			AsyncLoop.Run("PeriodicLog", (cancellation) =>
			{
				// TODO: move stats to each of its components
				StringBuilder benchLogs = new StringBuilder();

				benchLogs.AppendLine("======Node stats====== " + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture) + " agent " + this.ConnectionManager.Parameters.UserAgent);
				benchLogs.AppendLine("Headers.Height: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.Height.ToString().PadRight(8) + " Headers.Hash: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.HashBlock);

				if (this.ConsensusLoop != null)
				{
					benchLogs.AppendLine("Consensus.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.Height.ToString().PadRight(8) + " Consensus.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.HashBlock);
				}

				if (this._ChainBehaviorState.HighestPersistedBlock != null)
				{
					benchLogs.AppendLine("Store.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.Height.ToString().PadRight(8) + " Store.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.HashBlock);
				}

				benchLogs.AppendLine();

				if (this.MempoolManager != null)
				{
					benchLogs.AppendLine("======Mempool======");
					benchLogs.AppendLine(this.MempoolManager.PerformanceCounter.ToString());
				}

				benchLogs.AppendLine("======Connection======");
				benchLogs.AppendLine(this.ConnectionManager.GetNodeStats());
				Logs.Bench.LogInformation(benchLogs.ToString());
				return Task.CompletedTask;
			},
			this.GlobalCancellation.Cancellation.Token,
			repeatEvery: TimeSpans.FiveSeconds,
			startAfter: TimeSpans.FiveSeconds);
		}

		public void WaitDisposed()
		{
			_isDisposed.WaitOne();
			Dispose();
		}

	    private bool _isDisposedValue;
		private bool _hasExited;
		private Exception _uncatchedException;

		public bool HasExited
		{
			get
			{
				return _hasExited;
			}
		}

		public void Dispose()
		{
			if (this.IsDisposed)
				return;
            this._isDisposedValue = true;
			Logs.FullNode.LogInformation("Closing node pending...");

		    if (!this._stopped)
		    {
		        try
		        {
		            this.StopAsync().GetAwaiter().GetResult();
		        }
		        catch (Exception ex)
		        {
		            _logger?.LogError(ex.Message);
		        }
		    }

            _isStarted.WaitOne();
            //if (this.GlobalCancellation != null)
            //{
            //	this.GlobalCancellation.Cancellation.Cancel();

            //	ConnectionManager.Dispose();
            //	foreach (var dispo in _Resources)
            //		dispo.Dispose();

            //	DisposeFeatures();
            //}
            this._isDisposed.Set();
            this._hasExited = true;
        }

		public void ThrowIfUncatchedException()
		{
			if (this._uncatchedException != null)
			{
				var ex = this._uncatchedException;
				var aex = this._uncatchedException as AggregateException;
				if (aex != null)
					ex = aex.InnerException;
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
		}
	}
}
