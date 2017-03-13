using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.RequiredNodeComponents {
   public class ConnectionParametersService : IFullNodeFeature {
      private PeriodicTask _flushAddressManagerTask;
      private NodeArgs _nodeArgs;
      private CancellationTokenSource _cancellationToken = new CancellationTokenSource();
      private AddressManagerService _addressManagerService;
      private ChainService _chainService;

      public ConnectionManager ConnectionManager { get; private set; }

      public ConnectionParametersService(NodeArgs nodeArgs, AddressManagerService addressManagerService, ChainService chainService) {
         if (nodeArgs == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         if (addressManagerService == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         _nodeArgs = nodeArgs;
         _addressManagerService = addressManagerService;
         _chainService = chainService;
      }

      public void Start(FullNode fullNodeInstance) {
         var dataFolder = new DataFolder(_nodeArgs.DataDir);
         var network = _nodeArgs.GetNetwork();

         var chain = fullNodeInstance.Chain; //should be taken by the ChainService (need a public Chain property)
         var chainBehaviorState = fullNodeInstance.ChainBehaviorState;

         // == Connection == 
         var connectionParameters = new NodeConnectionParameters();
         connectionParameters.IsRelay = _nodeArgs.Mempool.RelayTxes;
         connectionParameters.Services = (_nodeArgs.Store.Prune ? NodeServices.Nothing : NodeServices.Network) | NodeServices.NODE_WITNESS;
         connectionParameters.TemplateBehaviors.Add(new BlockStore.ChainBehavior(chain, chainBehaviorState));
         connectionParameters.TemplateBehaviors.Add(new AddressManagerBehavior(_addressManagerService.AddressManager));

         ConnectionManager = new ConnectionManager(network, connectionParameters, _nodeArgs.ConnectionManager);
         var blockPuller = new NodesBlockPuller(chain, ConnectionManager.ConnectedNodes);
         connectionParameters.TemplateBehaviors.Add(new NodesBlockPuller.NodesBlockPullerBehavior(blockPuller));
      }



      public void Stop(FullNode fullNodeInstance) {
         _cancellationToken.Cancel();
         _flushAddressManagerTask.RunOnce();
         Logs.FullNode.LogInformation("FlushAddressManager stopped");
      }
   }


   internal static class ConnectionParametersServiceCollectionExtension {
      public static IServiceCollection AddConnectionParametersService(this IServiceCollection services) {
         services.AddSingleton<IFullNodeFeature, ConnectionParametersService>();

         return services;
      }
   }
}
