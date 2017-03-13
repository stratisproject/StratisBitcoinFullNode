using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.RequiredNodeComponents {
   public class RequiredComponentsFeature : IFullNodeFeature {
      private List<IDisposable> _disposableResources = new List<IDisposable>();

      public BlockStore.ChainBehavior.ChainState ChainBehaviorState { get; private set; }
      private NodeArgs _nodeArgs;


      public DateTimeProvider DateTimeProvider { get; private set; }


      public ConcurrentChain Chain { get; private set; }
      private PeriodicTask _flushChainTask;
      private CancellationTokenSource _cancellationToken = new CancellationTokenSource();


      private PeriodicTask _flushAddressManagerTask;
      public AddressManager AddressManager { get; private set; }


      public NodeConnectionParameters ConnectionParameters { get; private set; }
      public ConnectionManager ConnectionManager { get; private set; }

      //blockStore
      private BlockStore.BlockRepository _blockRepository;
      public BlockStoreManager BlockStoreManager { get; private set; }
      public ConsensusLoop ConsensusLoop { get; private set; }

      private BlockStoreCache _blockStoreCache;
      private NodesBlockPuller _blockPuller;

      public RequiredComponentsFeature(NodeArgs nodeArgs) {
         if (nodeArgs == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         _nodeArgs = nodeArgs;
      }

      public void Start(FullNode fullNodeInstance) {
         var dataFolder = new DataFolder(_nodeArgs.DataDir);
         var network = _nodeArgs.GetNetwork();

         this.DateTimeProvider = new Bitcoin.DateTimeProvider();


         StartConnectionManager(fullNodeInstance, dataFolder, network);

         StartAddressManager(fullNodeInstance, dataFolder, network);
         StartChain(fullNodeInstance, dataFolder, network);
         StartBlockStore(fullNodeInstance, dataFolder, network);
         StartNodePuller(fullNodeInstance, dataFolder, network);
         StartConsensusValidator(fullNodeInstance, dataFolder, network);

         WireComponentsToFullNode(fullNodeInstance);
      }

      /// <summary>
      /// wires the various components to the FullNode instance and register them as FullNode.Services
      /// </summary>
      /// <param name="fullNodeInstance"></param>
      private void WireComponentsToFullNode(FullNode fullNodeInstance) {



      }

      private void StartNodePuller(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         _blockPuller = new NodesBlockPuller(this.Chain, ConnectionManager.ConnectedNodes);
         AddNodeBehavior(new NodesBlockPuller.NodesBlockPullerBehavior(_blockPuller));
      }

      private void StartConsensusValidator(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         var consensusValidator = new ConsensusValidator(network.Consensus);
         ConsensusLoop = new ConsensusLoop(consensusValidator, Chain, fullNodeInstance.CoinView, _blockPuller);
         this.ChainBehaviorState.HighestValidatedPoW = ConsensusLoop.Tip;

         var flags = ConsensusLoop.GetFlags();
         if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
            ConnectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);

         fullNodeInstance.ConsensusLoop = ConsensusLoop;
      }

      private void StartBlockStore(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         // === BlockSgtore ===
         _blockRepository = AutoDispose(new BlockStore.BlockRepository(network, dataFolder.BlockPath));
         _blockStoreCache = AutoDispose(new BlockStoreCache(_blockRepository));

         var lightBlockPuller = new BlockingPuller(this.Chain, this.ConnectionManager.ConnectedNodes);
         var blockStoreLoop = new BlockStoreLoop(this.Chain, this.ConnectionManager,
            _blockRepository, this.DateTimeProvider, _nodeArgs, this.ChainBehaviorState, this._cancellationToken, lightBlockPuller);
         BlockStoreManager = new BlockStoreManager(this.Chain, this.ConnectionManager,
            _blockRepository, this.DateTimeProvider, _nodeArgs, this.ChainBehaviorState, blockStoreLoop);

         AddNodeBehavior(new BlockStoreBehavior(this.Chain, this.BlockStoreManager.BlockRepository, _blockStoreCache));
         AddNodeBehavior(new BlockingPuller.BlockingPullerBehavior(lightBlockPuller));

         fullNodeInstance.Signals.Blocks.Subscribe(new BlockStoreSignaled(blockStoreLoop, this.Chain, this._nodeArgs, this.ChainBehaviorState, this.ConnectionManager, this._cancellationToken));
      }

      private void StartConnectionManager(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         ConnectionParameters = new NodeConnectionParameters();
         ConnectionParameters.IsRelay = _nodeArgs.Mempool.RelayTxes;
         ConnectionParameters.Services = (_nodeArgs.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;

         ConnectionManager = AutoDispose(new ConnectionManager(network, ConnectionParameters, _nodeArgs.ConnectionManager));
         fullNodeInstance.ConnectionManager = ConnectionManager;
      }

      private void StartChain(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         if (!Directory.Exists(dataFolder.ChainPath)) {
            Logs.FullNode.LogInformation("Creating " + dataFolder.ChainPath);
            Directory.CreateDirectory(dataFolder.ChainPath);
         }

         var chainRepository = AutoDispose(new ChainRepository(dataFolder.ChainPath));

         Logs.FullNode.LogInformation("Loading chain");

         Chain = chainRepository.GetChain().GetAwaiter().GetResult();
         if (Chain == null) {
            Chain = new ConcurrentChain(network);
         }

         Guard.Assert(Chain.Genesis.HashBlock == network.GenesisHash); // can't swap networks
         Logs.FullNode.LogInformation("Chain loaded at height " + Chain.Height);

         _flushChainTask = new PeriodicTask("FlushChain", (cancellation) => {
            chainRepository.Save(Chain);
         })
         .Start(_cancellationToken.Token, TimeSpan.FromMinutes(5.0), true);


         ChainBehaviorState = new BlockStore.ChainBehavior.ChainState(fullNodeInstance);

         AddNodeBehavior(new BlockStore.ChainBehavior(this.Chain, this.ChainBehaviorState));

         //set node chain and chainrepository, but probably the fullnode should discover them itself in its start method using DI
         fullNodeInstance.Chain = Chain;
         fullNodeInstance.ChainRepository = chainRepository;
         fullNodeInstance.ChainBehaviorState = ChainBehaviorState;
      }

      private void StartAddressManager(FullNode fullNodeInstance, DataFolder dataFolder, Network network) {
         if (!File.Exists(dataFolder.AddrManFile)) {
            Logs.FullNode.LogInformation($"Creating {dataFolder.AddrManFile}");
            AddressManager = new AddressManager();
            AddressManager.SavePeerFile(dataFolder.AddrManFile, network);
            Logs.FullNode.LogInformation("Created");
         }
         else {
            Logs.FullNode.LogInformation("Loading  {dataFolder.AddrManFile}");
            AddressManager = AddressManager.LoadPeerFile(dataFolder.AddrManFile);
            Logs.FullNode.LogInformation("Loaded");
         }

         if (AddressManager.Count == 0) {
            Logs.FullNode.LogInformation("AddressManager is empty, discovering peers...");
         }

         _flushAddressManagerTask = new PeriodicTask("FlushAddressManager", (cancellation) => {
            AddressManager.SavePeerFile(dataFolder.AddrManFile, network);
         })
        .Start(_cancellationToken.Token, TimeSpan.FromMinutes(5.0), true);


         AddNodeBehavior(new AddressManagerBehavior(this.AddressManager));

         //set node chain and chainrepository, but probably the fullnode should discover them itself in its start method using DI
         fullNodeInstance.AddressManager = AddressManager;
      }



      public void Stop(FullNode fullNodeInstance) {
         _cancellationToken.Cancel();

         Logs.FullNode.LogInformation("FlushAddressManager stopped");
         _flushAddressManagerTask?.RunOnce();


         Logs.FullNode.LogInformation("FlushChain stopped");
         _flushChainTask?.RunOnce();


         Logs.FullNode.LogInformation("Flushing BlockStore...");
         this.BlockStoreManager.BlockStoreLoop.Flush().GetAwaiter().GetResult();

         foreach (var disposable in _disposableResources) {
            disposable.Dispose();
         }
      }


      #region Helpers
      private void AddNodeBehavior(INodeBehavior behavior) {
         ConnectionParameters.TemplateBehaviors.Add(behavior);
      }

      public TDisposable AutoDispose<TDisposable>(TDisposable resource) where TDisposable : IDisposable {
         _disposableResources.Add(resource);
         return resource;
      }
      #endregion

   }



   internal static class RequiredComponentsIServiceCollectionExtension {
      public static IServiceCollection AddRequiredComponentsFeature(this IServiceCollection services) {
         services.AddSingleton<IFullNodeFeature, RequiredComponentsFeature>();

         return services;
      }
   }
}
