using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.RequiredNodeComponents {
   public class ChainService : IFullNodeFeature {
      public ConcurrentChain Chain { get; private set; }
      private ChainRepository _chainRepository;
      private PeriodicTask _flushChainTask;
      private NodeArgs _nodeArgs;
      private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

      public ChainService(ChainRepository chainRepository, NodeArgs nodeArgs) {
         if (chainRepository == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         if (nodeArgs == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         _chainRepository = chainRepository;
         _nodeArgs = nodeArgs;
      }

      public void Start(FullNode fullNodeInstance) {
         var dataFolder = new DataFolder(_nodeArgs.DataDir);
         var network = _nodeArgs.GetNetwork();

         if (!Directory.Exists(dataFolder.ChainPath)) {
            Logs.FullNode.LogInformation("Creating " + dataFolder.ChainPath);
            Directory.CreateDirectory(dataFolder.ChainPath);
         }

         Logs.FullNode.LogInformation("Loading chain");

         Chain = _chainRepository.GetChain().GetAwaiter().GetResult();
         if (Chain == null) {
            Chain = new ConcurrentChain(network);
         }

         Guard.Assert(Chain.Genesis.HashBlock == network.GenesisHash); // can't swap networks
         Logs.FullNode.LogInformation("Chain loaded at height " + Chain.Height);

         _flushChainTask = new PeriodicTask("FlushChain", (cancellation) => {
            _chainRepository.Save(Chain);
         })
         .Start(_cancellationToken.Token, TimeSpan.FromMinutes(5.0), true);


         //set node chain and chainrepository, but probably the fullnode should discover them itself in its start method using DI
         fullNodeInstance.Chain = Chain;
         fullNodeInstance.ChainRepository = _chainRepository;
      }



      public void Stop(FullNode fullNodeInstance) {
         _cancellationToken.Cancel();
         _flushChainTask.RunOnce();
         Logs.FullNode.LogInformation("FlushChain stopped");

         _chainRepository?.Dispose();
      }
   }



   internal static class ChainIServiceCollectionExtension {
      public static IServiceCollection AddChainService(this IServiceCollection services) {
         ///in future, the concrete ChainRepository could be pluggable, maybe adding a ChainRepositoryFactory 
         ///and using that to create the ChainRepository instance
         services.AddSingleton<ChainRepository>(serviceProvider => {
            var nodeArgs = serviceProvider.GetService<NodeArgs>();
            var dataFolder = new DataFolder(nodeArgs.DataDir);

            var chainRepository = new ChainRepository(dataFolder.ChainPath);

            return chainRepository;
         });

         services.AddSingleton<IFullNodeFeature, ChainService>();

         return services;
      }
   }
}
