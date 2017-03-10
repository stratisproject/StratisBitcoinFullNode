using Stratis.Bitcoin.MemoryPool;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using System;

namespace Stratis.Bitcoin.Builder {
   public class MempoolFeature : IFullNodeFeature {
      private static IServiceCollection _serviceCollection;
      private FullNode _nodeInstance;
      private bool _nodeServiceAreSet = false;

      internal static void RegisterNeededServices(IServiceCollection services) {
         _serviceCollection = services;

         services.AddSingleton<MempoolScheduler>();
         services.AddSingleton<TxMempool>();
         services.AddSingleton<FeeRate>(MempoolValidator.MinRelayTxFee);
         services.AddSingleton<MempoolValidator>();
         services.AddSingleton<MempoolOrphans>();
         services.AddSingleton<MempoolManager>();
         services.AddSingleton<MempoolBehavior>();
         services.AddSingleton<MempoolSignaled>();
      }

      public void Start(FullNode nodeInstance) {
         _nodeInstance = nodeInstance;

         EnsureNodeServiceAreSet(nodeInstance);

         _nodeInstance.ConnectionManager.Parameters.TemplateBehaviors.Add(_nodeInstance.Services.GetService<MempoolBehavior>());
         _nodeInstance.Signals.Blocks.Subscribe(_nodeInstance.Services.GetService<MempoolSignaled>());
         _nodeInstance.MempoolManager = _nodeInstance.Services.GetService<MempoolManager>();
      }

      public void Stop() {
         //todo cleanup
      }


      private void EnsureNodeServiceAreSet(FullNode nodeInstance) {
         if (!_nodeServiceAreSet) {

            ///since actually those singletons needs an initialized node, can be used only when node is starting
            ///but anyway this method is just temporary because all needed services must be wired at node initialization 
            ///or handled as node features themselves

            // TODO: some of this types are required and will move to a NodeBuilder implementations
            // temporary types
            _serviceCollection.AddSingleton(nodeInstance.Chain);
            _serviceCollection.AddSingleton(nodeInstance.Args);
            _serviceCollection.AddSingleton(nodeInstance.ConnectionManager);
            _serviceCollection.AddSingleton(nodeInstance.CoinView);
            _serviceCollection.AddSingleton(nodeInstance.ConsensusLoop.Validator);
            _serviceCollection.AddSingleton(nodeInstance.DateTimeProvider);
            _serviceCollection.AddSingleton(nodeInstance.ChainBehaviorState);
            _serviceCollection.AddSingleton(nodeInstance.GlobalCancellation);

            nodeInstance.UpdateApplicationServiceProvider();
            _nodeServiceAreSet = true;
         }
      }
   }
}
