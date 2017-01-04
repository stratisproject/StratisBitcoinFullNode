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

		CancellationTokenSource _Cancellation;

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
			CoinView = new CachedCoinView(coinviewDB);
			_Cancellation = new CancellationTokenSource();
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

			StartFlushAddrManThread();
			StartFlushChainThread();

			var connectionParameters = new NodeConnectionParameters();
			connectionParameters.TemplateBehaviors.Add(new ChainBehavior(Chain));
			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			ConnectionManager = new ConnectionManager(Network, connectionParameters, _Args.ConnectionManager);
			ConsensusLoop = new ConsensusLoop(new ConsensusValidator(Network.Consensus), Chain, CoinView, new NodesBlockPuller(Chain, ConnectionManager.ConnectedNodes));
			new Thread(RunLoop)
			{
				Name = "Consensus Loop"
			}.Start();
			_IsStarted.Set();
		}

		void RunLoop()
		{
			var stack = new CoinViewStack(CoinView);
			var cache = stack.Find<CachedCoinView>();
			var dbreeze = stack.Find<DBreezeCoinView>();
			var bottom = stack.Bottom;

			var lookaheadPuller = ConsensusLoop.Puller as LookaheadBlockPuller;

			var lastSnapshot = ConsensusLoop.Validator.PerformanceCounter.Snapshot();
			var lastSnapshot2 = dbreeze == null ? null : dbreeze.PerformanceCounter.Snapshot();
			var lastSnapshot3 = cache == null ? null : cache.PerformanceCounter.Snapshot();
			foreach(var block in ConsensusLoop.Execute())
			{
				if(_IsDisposed.WaitOne(0))
					break;
				if(block.Error != null)
				{
					//TODO: 
					Logs.FullNode.LogError("Block rejected: " + block.Error.Message);
				}
				if((DateTimeOffset.UtcNow - lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0))
				{
					StringBuilder benchLogs = new StringBuilder();

					if(lookaheadPuller != null)
					{
						benchLogs.AppendLine("ActualLookahead :\t" + lookaheadPuller.ActualLookahead + " blocks");
						benchLogs.AppendLine("Median Downloaded :\t" + lookaheadPuller.MedianDownloadCount + " blocks");
					}
					benchLogs.AppendLine("Persistent Tip :\t" + Chain.GetBlock(bottom.GetBlockHashAsync().Result).Height);
					if(cache != null)
					{
						benchLogs.AppendLine("Cache Tip :\t" + Chain.GetBlock(cache.GetBlockHashAsync().Result).Height);
						benchLogs.AppendLine("Cache entries :\t" + cache.CacheEntryCount);
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
					Logs.Bench.LogInformation(benchLogs.ToString());
				}
			}
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
		public bool IsDisposed
		{
			get
			{
				return _IsDisposed.WaitOne(0);
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

		public void Dispose()
		{
			if(IsDisposed)
				return;
			Logs.FullNode.LogInformation("Closing node pending...");
			_IsStarted.WaitOne();
			if(_Cancellation != null)
			{
				_Cancellation.Cancel();
				FlushAddrmanTask.RunOnce();
				Logs.FullNode.LogInformation("FlushAddrMan stopped");
				FlushChainTask.RunOnce();
				Logs.FullNode.LogInformation("FlushChain stopped");
				ConnectionManager.Dispose();
				foreach(var dispo in _Resources)
					dispo.Dispose();
			}
			_IsDisposed.Set();
		}
	}
}
