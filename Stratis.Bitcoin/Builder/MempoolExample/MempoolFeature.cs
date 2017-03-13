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

         _nodeInstance.ConnectionManager.Parameters.TemplateBehaviors.Add(_nodeInstance.Services.GetService<MempoolBehavior>());
         _nodeInstance.Signals.Blocks.Subscribe(_nodeInstance.Services.GetService<MempoolSignaled>());
         _nodeInstance.MempoolManager = _nodeInstance.Services.GetService<MempoolManager>();
      }

      public void Stop(FullNode nodeInstance) {
         //todo cleanup
      }
   }
}
