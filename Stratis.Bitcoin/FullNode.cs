using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Miner;

namespace Stratis.Bitcoin {

   public partial class FullNode : IDisposable {
      NodeArgs _Args;
      public NodeArgs Args {
         get {
            return _Args;
         }
      }

      public FullNode(NodeArgs args) {
         if (args == null)
            throw new ArgumentNullException("args");
         _Args = args;
         Network = _Args.GetNetwork();
      }

      public Network Network {
         get;
         internal set;
      }

      public CoinView CoinView {
         get; set;
      }

      public DataFolder DataFolder {
         get; set;
      }

      public DateTimeProvider DateTimeProvider {
         get; set;
      }

      public bool IsInitialBlockDownload() {
         //if (fImporting || fReindex)
         //	return true;
         if (this.ConsensusLoop.Tip == null)
            return true;
         if (this.ConsensusLoop.Tip.ChainWork < this.Network.Consensus.MinimumChainWork)
            return true;
         if (this.ConsensusLoop.Tip.Header.BlockTime.ToUnixTimeSeconds() < (this.DateTimeProvider.GetTime() - this.Args.MaxTipAge))
            return true;
         return false;
      }


      List<IDisposable> _Resources = new List<IDisposable>();
      public void Start() {
         if (IsDisposed)
            throw new ObjectDisposedException("FullNode");
         _IsStarted.Reset();
         DataFolder = new DataFolder(_Args.DataDir);
         var coinviewDB = new DBreezeCoinView(Network, DataFolder.CoinViewPath);
         _Resources.Add(coinviewDB);
         CoinView = new CachedCoinView(coinviewDB) { MaxItems = _Args.Cache.MaxItems };


         _Cancellation = new CancellationTokenSource();
         this.GlobalCancellation = new CancellationProvider() { Cancellation = _Cancellation };

         if (_Args.RPC != null) {
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
         this.DateTimeProvider = DateTimeProvider.Default;

         // === Miner ===
         this.Miner = new Mining(this, this.DateTimeProvider);

         StartBuilderStuff();

         _ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;
         ConnectionManager.Start();

         new Thread(RunLoop) {
            Name = "Consensus Loop"
         }.Start();
         _IsStarted.Set();

         this.StartPeriodicLog();
      }

      private BlockStore.ChainBehavior.ChainState _ChainBehaviorState;
      public BlockStore.ChainBehavior.ChainState ChainBehaviorState {
         get { return _ChainBehaviorState; }
         internal set { _ChainBehaviorState=value; }
      }

      public class ConsensusStats {
         private readonly FullNode fullNode;
         private CoinViewStack stack;
         private CachedCoinView cache;
         private DBreezeCoinView dbreeze;
         private CoinView bottom;

         private LookaheadBlockPuller lookaheadPuller;
         private ConsensusPerformanceSnapshot lastSnapshot;
         private BackendPerformanceSnapshot lastSnapshot2;
         private CachePerformanceSnapshot lastSnapshot3;

         public ConsensusStats(FullNode fullNode, CoinViewStack stack) {
            this.fullNode = fullNode;

            stack = new CoinViewStack(fullNode.CoinView);
            cache = stack.Find<CachedCoinView>();
            dbreeze = stack.Find<DBreezeCoinView>();
            bottom = stack.Bottom;

            lookaheadPuller = fullNode.ConsensusLoop.Puller as LookaheadBlockPuller;

            lastSnapshot = fullNode.ConsensusLoop.Validator.PerformanceCounter.Snapshot();
            lastSnapshot2 = dbreeze?.PerformanceCounter.Snapshot();
            lastSnapshot3 = cache?.PerformanceCounter.Snapshot();
         }

         public bool CanLog {
            get {
               return this.fullNode._ChainBehaviorState.IsInitialBlockDownload &&
                  (DateTimeOffset.UtcNow - lastSnapshot.Taken) > TimeSpan.FromSeconds(5.0);
            }
         }

         public void Log() {
            StringBuilder benchLogs = new StringBuilder();

            if (lookaheadPuller != null) {
               benchLogs.AppendLine("======Block Puller======");
               benchLogs.AppendLine("Lookahead:".PadRight(Logs.ColumnLength) + lookaheadPuller.ActualLookahead + " blocks");
               benchLogs.AppendLine("Downloaded:".PadRight(Logs.ColumnLength) + lookaheadPuller.MedianDownloadCount + " blocks");
               benchLogs.AppendLine("==========================");
            }
            benchLogs.AppendLine("Persistent Tip:".PadRight(Logs.ColumnLength) + this.fullNode.Chain.GetBlock(bottom.GetBlockHashAsync().Result).Height);
            if (cache != null) {
               benchLogs.AppendLine("Cache Tip".PadRight(Logs.ColumnLength) + this.fullNode.Chain.GetBlock(cache.GetBlockHashAsync().Result).Height);
               benchLogs.AppendLine("Cache entries".PadRight(Logs.ColumnLength) + cache.CacheEntryCount);
            }

            var snapshot = this.fullNode.ConsensusLoop.Validator.PerformanceCounter.Snapshot();
            benchLogs.AppendLine((snapshot - lastSnapshot).ToString());
            lastSnapshot = snapshot;

            if (dbreeze != null) {
               var snapshot2 = dbreeze.PerformanceCounter.Snapshot();
               benchLogs.AppendLine((snapshot2 - lastSnapshot2).ToString());
               lastSnapshot2 = snapshot2;
            }
            if (cache != null) {
               var snapshot3 = cache.PerformanceCounter.Snapshot();
               benchLogs.AppendLine((snapshot3 - lastSnapshot3).ToString());
               lastSnapshot3 = snapshot3;
            }
            benchLogs.AppendLine(this.fullNode.ConnectionManager.GetStats());
            Logs.Bench.LogInformation(benchLogs.ToString());
         }
      }

      void RunLoop() {
         try {
            var stack = new CoinViewStack(CoinView);
            var cache = stack.Find<CachedCoinView>();
            var stats = new ConsensusStats(this, stack);

            ChainedBlock lastTip = ConsensusLoop.Tip;
            foreach (var block in ConsensusLoop.Execute(_Cancellation.Token)) {
               bool reorg = false;
               if (ConsensusLoop.Tip.FindFork(lastTip) != lastTip) {
                  reorg = true;
                  Logs.FullNode.LogInformation("Reorg detected, rewinding from " + lastTip.Height + " (" + lastTip.HashBlock + ") to " + ConsensusLoop.Tip.Height + " (" + ConsensusLoop.Tip.HashBlock + ")");
               }
               lastTip = ConsensusLoop.Tip;
               _Cancellation.Token.ThrowIfCancellationRequested();
               if (block.Error != null) {
                  Logs.FullNode.LogError("Block rejected: " + block.Error.Message);

                  //Pull again
                  ConsensusLoop.Puller.SetLocation(ConsensusLoop.Tip);

                  if (block.Error == ConsensusErrors.BadWitnessNonceSize) {
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

               if (block.Error == null) {
                  _ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;
                  if (Chain.Tip.HashBlock == block.ChainedBlock.HashBlock) {
                     var unused = cache.FlushAsync();
                  }

                  this.Signals.Blocks.Broadcast(block.Block);
               }

               // TODO: replace this with a signalling object
               if (stats.CanLog)
                  stats.Log();
            }
         }
         catch (Exception ex) //TODO: Barbaric clean exit
         {
            if (ex is OperationCanceledException) {
               if (_Cancellation.IsCancellationRequested)
                  return;
            }
            if (!IsDisposed) {
               Logs.FullNode.LogCritical(new EventId(0), ex, "Consensus loop unhandled exception (Tip:" + ConsensusLoop.Tip?.Height + ")");
               _UncatchedException = ex;
               Dispose();
            }
         }
      }

      public Mining Miner {
         get; set;
      }

      public Signals Signals {
         get; set;
      }

      public ConsensusLoop ConsensusLoop {
         get; set;
      }

      public IWebHost RPCHost {
         get; set;
      }

      public ConnectionManager ConnectionManager {
         get; set;
      }

      public MempoolManager MempoolManager {
         get; set;
      }

      public AddressManager AddressManager {
         get; set;
      }

      public ChainRepository ChainRepository {
         get; set;
      }

      public BlockStoreManager BlockStoreManager {
         get; set;
      }

      /// <summary>
      /// The longest PoW chain
      /// </summary>
      public ConcurrentChain Chain {
         get; set;
      }

      public CancellationProvider GlobalCancellation {
         get; set;
      }
      public class CancellationProvider {
         public CancellationTokenSource Cancellation { get; set; }
      }

      ManualResetEvent _IsDisposed = new ManualResetEvent(false);
      ManualResetEvent _IsStarted = new ManualResetEvent(false);
      CancellationTokenSource _Cancellation = new CancellationTokenSource();
      public bool IsDisposed {
         get {
            return _IsDisposedValue;
         }
      }

      private void StartPeriodicLog() {
         AsyncLoop.Run("PeriodicLog", (cancellation) => {
            // TODO: move stats to each of its components 

            StringBuilder benchLogs = new StringBuilder();

            benchLogs.AppendLine("======Consensus====== " + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture));
            benchLogs.AppendLine("Headers.Height: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.Height.ToString().PadRight(8) + " Headers.Hash: ".PadRight(Logs.ColumnLength + 3) + this.Chain.Tip.HashBlock);
            benchLogs.AppendLine("Consensus.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.Height.ToString().PadRight(8) + " Consensus.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestValidatedPoW.HashBlock);
            benchLogs.AppendLine("Store.Height: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.Height.ToString().PadRight(8) + " Store.Hash: ".PadRight(Logs.ColumnLength + 3) + this._ChainBehaviorState.HighestPersistedBlock.HashBlock);
            benchLogs.AppendLine();

            benchLogs.AppendLine("======Mempool======");
            benchLogs.AppendLine(this.MempoolManager.PerformanceCounter.ToString());

            benchLogs.AppendLine("======Connection======");
            benchLogs.AppendLine(this.ConnectionManager.GetNodeStats());
            Logs.Bench.LogInformation(benchLogs.ToString());
            return Task.CompletedTask;
         },
         _Cancellation.Token,
         repeateEvery: TimeSpans.FiveSeconds,
         startAfter: TimeSpans.FiveSeconds);
      }

      public void WaitDisposed() {
         _IsDisposed.WaitOne();
         Dispose();
      }

      bool _IsDisposedValue;


      private bool _HasExited;
      private Exception _UncatchedException;

      public bool HasExited {
         get {
            return _HasExited;
         }
      }

      public void Dispose() {
         if (IsDisposed)
            return;
         _IsDisposedValue = true;
         Logs.FullNode.LogInformation("Closing node pending...");
         _IsStarted.WaitOne();
         if (_Cancellation != null) {
            _Cancellation.Cancel();

            var cache = CoinView as CachedCoinView;
            if (cache != null) {
               Logs.FullNode.LogInformation("Flushing Cache CoinView...");
               cache.FlushAsync().GetAwaiter().GetResult();
            }

            foreach (var dispo in _Resources)
               dispo.Dispose();

            DisposeBuilderStuff();
         }
         _IsDisposed.Set();
         _HasExited = true;
      }

      public void ThrowIfUncatchedException() {
         if (_UncatchedException != null) {
            var ex = _UncatchedException;
            var aex = _UncatchedException as AggregateException;
            if (aex != null)
               ex = aex.InnerException;
            ExceptionDispatchInfo.Capture(ex).Throw();
         }
      }
   }
}
