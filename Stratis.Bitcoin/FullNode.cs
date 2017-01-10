using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Stratis.Bitcoin.RPC;
using NBitcoin;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Consensus;
using NBitcoin.Protocol;
using Microsoft.AspNetCore.Hosting.Internal;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.BlockPulling;
using System.Text;
using System.Runtime.ExceptionServices;
using Stratis.Bitcoin.BlockStore;

namespace Stratis.Bitcoin
{
	public class FullNode : IDisposable
	{
		NodeArgs _Args;
		public NodeArgs Args
		{
			get
			{
				return _Args;
			}
		}

		public FullNode(NodeArgs args)
		{
			if(args == null)
				throw new ArgumentNullException("args");
			_Args = args;
			Network = _Args.GetNetwork();
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

		List<IDisposable> _Resources = new List<IDisposable>();
		public void Start()
		{
			if(IsDisposed)
				throw new ObjectDisposedException("FullNode");
			_IsStarted.Reset();
			DataFolder = new DataFolder(_Args.DataDir);
			var coinviewDB = new DBreezeCoinView(Network, DataFolder.CoinViewPath);
			_Resources.Add(coinviewDB);
			CoinView = new CachedCoinView(coinviewDB) { MaxItems = _Args.Cache.MaxItems };

			
			_Cancellation = new CancellationTokenSource();
			StartFlushAddrManThread();
			StartFlushChainThread();

			if(_Args.RPC != null)
			{
				RPCHost = new WebHostBuilder()
				.UseKestrel()
				.ForFullNode(this)
				.UseUrls(_Args.RPC.GetUrls())
				.UseIISIntegration()
				.UseStartup<RPC.Startup>()
				.Build();
				RPCHost.Start();
				_Resources.Add(RPCHost);
				Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + String.Join(Environment.NewLine, _Args.RPC.GetUrls()));
			}

			if(AddressManager.Count == 0)
				Logs.FullNode.LogInformation("AddressManager is empty, discovering peers...");

			var connectionParameters = new NodeConnectionParameters();
			connectionParameters.TemplateBehaviors.Add(new ChainBehavior(Chain));
			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			ConnectionManager = new ConnectionManager(Network, connectionParameters, _Args.ConnectionManager);
			var blockPuller = new NodesBlockPuller(Chain, ConnectionManager.ConnectedNodes);
			connectionParameters.TemplateBehaviors.Add(new NodesBlockPuller.NodesBlockPullerBehavior(blockPuller));

			if (_Args.Prune == 0)
			{
				// TODO: later use the prune size to limit storage size
				BlockRepository = new BlockRepository(DataFolder.BlockPath);
				_Resources.Add(BlockRepository);
				connectionParameters.TemplateBehaviors.Add(new BlockStoreBehaviour(this.Chain, this.BlockRepository));
			}

			ConnectionManager.Start();
			ConsensusLoop = new ConsensusLoop(new ConsensusValidator(Network.Consensus), Chain, CoinView, blockPuller);
			new Thread(RunLoop)
			{
				Name = "Consensus Loop"
			}.Start();
			_IsStarted.Set();
		}

		void RunLoop()
		{
			try
			{

				var stack = new CoinViewStack(CoinView);
				var cache = stack.Find<CachedCoinView>();
				var dbreeze = stack.Find<DBreezeCoinView>();
				var bottom = stack.Bottom;

				var lookaheadPuller = ConsensusLoop.Puller as LookaheadBlockPuller;

				var lastSnapshot = ConsensusLoop.Validator.PerformanceCounter.Snapshot();
				var lastSnapshot2 = dbreeze == null ? null : dbreeze.PerformanceCounter.Snapshot();
				var lastSnapshot3 = cache == null ? null : cache.PerformanceCounter.Snapshot();

				ChainedBlock lastTip = ConsensusLoop.Tip;
				foreach(var block in ConsensusLoop.Execute(_Cancellation.Token))
				{
					bool reorg = false;
					if(ConsensusLoop.Tip.FindFork(lastTip) != lastTip)
					{
						reorg = true;
						Logs.FullNode.LogInformation("Reorg detected, rewinding from " + lastTip.Height + " (" + lastTip.HashBlock + ") to " + ConsensusLoop.Tip.Height + " (" + ConsensusLoop.Tip.HashBlock + ")");
					}
					lastTip = ConsensusLoop.Tip;
					if(_IsDisposed.WaitOne(0))
						break;
					if(block.Error != null)
					{
						//TODO: 
						Logs.FullNode.LogError("Block rejected: " + block.Error.Message);
					}

					if (block.Error == null)
					{
						this.TryStoreBlock(block.Block, reorg);
					}

					if((DateTimeOffset.UtcNow - lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0))
					{
						StringBuilder benchLogs = new StringBuilder();

						if(lookaheadPuller != null)
						{
							benchLogs.AppendLine("======Block Puller======");
							benchLogs.AppendLine("Lookahead:".PadRight(Logs.ColumnLength) + lookaheadPuller.ActualLookahead + " blocks");
							benchLogs.AppendLine("Downloaded:".PadRight(Logs.ColumnLength) + lookaheadPuller.MedianDownloadCount + " blocks");
							benchLogs.AppendLine("==========================");
						}
						benchLogs.AppendLine("Persistent Tip:".PadRight(Logs.ColumnLength) + Chain.GetBlock(bottom.GetBlockHashAsync().Result).Height);
						if(cache != null)
						{
							benchLogs.AppendLine("Cache Tip".PadRight(Logs.ColumnLength) + Chain.GetBlock(cache.GetBlockHashAsync().Result).Height);
							benchLogs.AppendLine("Cache entries".PadRight(Logs.ColumnLength) + cache.CacheEntryCount);
						}

						var snapshot = ConsensusLoop.Validator.PerformanceCounter.Snapshot();
						benchLogs.AppendLine((snapshot - lastSnapshot).ToString());
						lastSnapshot = snapshot;

						if(dbreeze != null)
						{
							var snapshot2 = dbreeze.PerformanceCounter.Snapshot();
							benchLogs.AppendLine((snapshot2 - lastSnapshot2).ToString());
							lastSnapshot2 = snapshot2;
						}
						if(cache != null)
						{
							var snapshot3 = cache.PerformanceCounter.Snapshot();
							benchLogs.AppendLine((snapshot3 - lastSnapshot3).ToString());
							lastSnapshot3 = snapshot3;
						}
						benchLogs.AppendLine(ConnectionManager.GetStats());
						Logs.Bench.LogInformation(benchLogs.ToString());
					}
				}
			}
			catch(Exception ex) //TODO: Barbaric clean exit
			{
				if(ex is OperationCanceledException)
				{
					if(_Cancellation.IsCancellationRequested)
						return;
				}
				if(!IsDisposed)
				{
					Logs.FullNode.LogCritical(new EventId(0), ex, "Consensus loop unhandled exception (Tip:" + ConsensusLoop.Tip?.Height + ")");
					_UncatchedException = ex;
					Dispose();
				}
			}
		}

		private Task TryStoreBlock(Block block, bool reorg)
		{
			if (reorg)
			{
				// TODO: delete blocks if reorg
				// this can be done periodically or 
				// on a separate loop not to block consensus
			}
			else
			{
				return this.BlockRepository?.PutAsync(block);
			}

			return Task.CompletedTask;
		}

		public ConsensusLoop ConsensusLoop
		{
			get; set;
		}



		public IWebHost RPCHost
		{
			get; set;
		}

		private void StartFlushChainThread()
		{
			if(!Directory.Exists(DataFolder.ChainPath))
			{
				Logs.FullNode.LogInformation("Creating " + DataFolder.ChainPath);
				Directory.CreateDirectory(DataFolder.ChainPath);
			}
			ChainRepository = new ChainRepository(DataFolder.ChainPath);
			_Resources.Add(ChainRepository);
			Logs.FullNode.LogInformation("Loading chain");
			Chain = ChainRepository.GetChain().GetAwaiter().GetResult();
			Chain = Chain ?? new ConcurrentChain(Network);
			Logs.FullNode.LogInformation("Chain loaded at height " + Chain.Height);
			FlushChainTask = new PeriodicTask("FlushChain", (cancellation) =>
			{
				ChainRepository.Save(Chain);
			}).Start(_Cancellation.Token);
		}

		public ConnectionManager ConnectionManager
		{
			get; set;
		}

		public AddressManager AddressManager
		{
			get; set;
		}

		public ChainRepository ChainRepository
		{
			get; set;
		}

		public BlockRepository BlockRepository
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

		ManualResetEvent _IsDisposed = new ManualResetEvent(false);
		ManualResetEvent _IsStarted = new ManualResetEvent(false);
		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		public bool IsDisposed
		{
			get
			{
				return _IsDisposedValue;
			}
		}

		private void StartFlushAddrManThread()
		{
			if(!File.Exists(DataFolder.AddrManFile))
			{
				Logs.FullNode.LogInformation("Creating " + DataFolder.AddrManFile);
				AddressManager = new AddressManager();
				AddressManager.SavePeerFile(DataFolder.AddrManFile, Network);
			}
			else
			{
				Logs.FullNode.LogInformation("Loading addrman");
				AddressManager = AddressManager.LoadPeerFile(DataFolder.AddrManFile);
				Logs.FullNode.LogInformation("Loaded");
			}
			FlushAddrmanTask = new PeriodicTask("FlushAddrMan", (cancellation) =>
			{
				AddressManager.SavePeerFile(DataFolder.AddrManFile, Network);
			}).Start(_Cancellation.Token);
		}

		public void WaitDisposed()
		{
			_IsDisposed.WaitOne();
			Dispose();
		}

		bool _IsDisposedValue;


		private bool _HasExited;
		private Exception _UncatchedException;

		public bool HasExited
		{
			get
			{
				return _HasExited;
			}
		}

		public void Dispose()
		{
			if(IsDisposed)
				return;
			_IsDisposedValue = true;
			Logs.FullNode.LogInformation("Closing node pending...");
			_IsStarted.WaitOne();
			if(_Cancellation != null)
			{
				_Cancellation.Cancel();
				FlushAddrmanTask.RunOnce();
				Logs.FullNode.LogInformation("FlushAddrMan stopped");
				FlushChainTask.RunOnce();
				Logs.FullNode.LogInformation("FlushChain stopped");

				var cache = CoinView as CachedCoinView;
				if(cache != null)
				{
					Logs.FullNode.LogInformation("Flushing Cache CoinView...");
					cache.FlushAsync().GetAwaiter().GetResult();
				}
				ConnectionManager.Dispose();
				foreach(var dispo in _Resources)
					dispo.Dispose();
			}
			_IsDisposed.Set();
			_HasExited = true;
		}

		public void ThrowIfUncatchedException()
		{
			if(_UncatchedException != null)
			{
				var ex = _UncatchedException;
				var aex = _UncatchedException as AggregateException;
				if(aex != null)
					ex = aex.InnerException;
				ExceptionDispatchInfo.Capture(ex).Throw();
			}
		}
	}
}
