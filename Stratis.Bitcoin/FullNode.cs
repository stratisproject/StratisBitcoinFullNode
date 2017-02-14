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
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Miner;

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

		public bool IsInitialBlockDownload()
		{
			//if (fImporting || fReindex)
			//	return true;
			if (this.ConsensusLoop.Tip == null)
				return true;
			if (this.ConsensusLoop.Tip.ChainWork < MinimumChainWork(this.Args))
				return true;
			if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (DateTime.UtcNow.UnixTimestamp() - this.Args.MaxTipAge))
				return true;
			return false;
		}

		private static uint256 MinimumChainWork(NodeArgs args)
		{
			// TODO: move this to Network.Consensus
			if (args.RegTest)
				return uint256.Zero;
			if (args.Testnet)
				return uint256.Parse("0x0000000000000000000000000000000000000000000000198b4def2baa9338d6");
			return uint256.Parse("0x0000000000000000000000000000000000000000002cb971dd56d1c583c20f90");
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

			this.Signals = new Signals();
			this._ChainBehaviorState = new BlockStore.ChainBehavior.ChainState(this);

			if(AddressManager.Count == 0)
				Logs.FullNode.LogInformation("AddressManager is empty, discovering peers...");

			var connectionParameters = new NodeConnectionParameters();
			connectionParameters.IsRelay = _Args.Mempool.RelayTxes;
			connectionParameters.Services = (Args.Prune ? NodeServices.Nothing :  NodeServices.Network) | NodeServices.NODE_WITNESS;
			connectionParameters.TemplateBehaviors.Add(new BlockStore.ChainBehavior(Chain, this.ChainBehaviorState));
			_ChainBehaviorState = connectionParameters.TemplateBehaviors.Find<BlockStore.ChainBehavior>().SharedState;
			connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(AddressManager));
			ConnectionManager = new ConnectionManager(Network, connectionParameters, _Args.ConnectionManager);
			var blockPuller = new NodesBlockPuller(Chain, ConnectionManager.ConnectedNodes, this._ChainBehaviorState);
			connectionParameters.TemplateBehaviors.Add(new NodesBlockPuller.NodesBlockPullerBehavior(blockPuller));

			// TODO: later use the prune size to limit storage size
			this.BlockStoreManager = new BlockStoreManager(this.Chain, this.ConnectionManager,
				new BlockRepository(DataFolder.BlockPath), DateTimeProvider.Default, _Args, this._ChainBehaviorState);
			_Resources.Add(this.BlockStoreManager.BlockRepository);
			connectionParameters.TemplateBehaviors.Add(new BlockStoreBehavior(this.Chain, this.BlockStoreManager.BlockRepository, this.BlockStoreManager));
			this.Signals.Blocks.Subscribe(new BlockStoreSignaled(this.BlockStoreManager, this.Chain));

			var consensusValidator = new ConsensusValidator(Network.Consensus);
			ConsensusLoop = new ConsensusLoop(consensusValidator, Chain, CoinView, blockPuller);
			this._ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;

			// create the memory pool
			var mempool = new TxMempool(MempoolValidator.MinRelayTxFee, _Args);
			var mempoolScheduler = new SchedulerPairSession();
			var mempoolValidator = new MempoolValidator(mempool, mempoolScheduler, consensusValidator, DateTimeProvider.Default, _Args, this.Chain, this.CoinView);
			var mempoollOrphans = new MempoolOrphans(mempoolScheduler, mempool, this.Chain, mempoolValidator, this.CoinView, DateTimeProvider.Default, _Args);
			this.MempoolManager = new MempoolManager(mempoolScheduler, mempool, this.Chain, mempoolValidator, mempoollOrphans, DateTimeProvider.Default, _Args);
			connectionParameters.TemplateBehaviors.Add(new MempoolBehavior(mempoolValidator, this.MempoolManager, mempoollOrphans, this.ConnectionManager, this.ChainBehaviorState));
			this.Signals.Blocks.Subscribe(new MempoolSignaled(this.MempoolManager, this.Chain));

			this.Miner = new Mining(this, DateTimeProvider.Default);

			var flags = ConsensusLoop.GetFlags();
			if(flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
				ConnectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);

			_ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;
			ConnectionManager.Start();

			new Thread(RunLoop)
			{
				Name = "Consensus Loop"
			}.Start();
			_IsStarted.Set();
		}

		private BlockStore.ChainBehavior.ChainState _ChainBehaviorState;
		public BlockStore.ChainBehavior.ChainState ChainBehaviorState
		{
			get { return _ChainBehaviorState; } 
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
					_Cancellation.Token.ThrowIfCancellationRequested();
					if(block.Error != null)
					{
						Logs.FullNode.LogError("Block rejected: " + block.Error.Message);

						//Pull again
						ConsensusLoop.Puller.SetLocation(ConsensusLoop.Tip);

						if(block.Error == ConsensusErrors.BadWitnessNonceSize)
						{
							Logs.FullNode.LogInformation("You probably need witness information, activating witness requirement for peers.");
							ConnectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);
							ConsensusLoop.Puller.RequestOptions(TransactionOptions.Witness);
							continue;
						}

						//Set the PoW chain back to ConsensusLoop.Tip
						Chain.SetTip(ConsensusLoop.Tip);
						//Since ChainBehavior check PoW, MarkBlockInvalid can't be spammed
						Logs.FullNode.LogError("Marking block as invalid");
						_ChainBehaviorState.MarkBlockInvalid(block.ChainedBlock.HashBlock);
					}

					if(block.Error == null)
					{
						_ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;
						if(Chain.Tip.HashBlock == block.ChainedBlock.HashBlock)
						{
							var unused = cache.FlushAsync();
						}

						this.Signals.Blocks.Broadcast(block.Block);
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
			Check.Assert(Chain.Genesis.HashBlock == Network.GenesisHash); // can't swap networks
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

		public MempoolManager MempoolManager
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
