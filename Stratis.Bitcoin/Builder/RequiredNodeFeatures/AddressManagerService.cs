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
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.RequiredNodeComponents {
   public class AddressManagerService : IFullNodeFeature {
      private PeriodicTask _flushAddressManagerTask;
      private NodeArgs _nodeArgs;
      private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

      public AddressManager AddressManager { get; private set; }

      public AddressManagerService(NodeArgs nodeArgs) {
         if (nodeArgs == null) {
            throw new ArgumentException(nameof(nodeArgs));
         }

         _nodeArgs = nodeArgs;
      }

      public void Start(FullNode fullNodeInstance) {
         var dataFolder = new DataFolder(_nodeArgs.DataDir);
         var network = _nodeArgs.GetNetwork();

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


         //set node chain and chainrepository, but probably the fullnode should discover them itself in its start method using DI
         fullNodeInstance.AddressManager= AddressManager;
      }



      public void Stop(FullNode fullNodeInstance) {
         _cancellationToken.Cancel();
         _flushAddressManagerTask.RunOnce();
         Logs.FullNode.LogInformation("FlushAddressManager stopped");
      }
   }


   internal static class AddressManagerIServiceCollectionExtension {
      public static IServiceCollection AddAddressManagerService(this IServiceCollection services) {
         services.AddSingleton<IFullNodeFeature, AddressManagerService>();

         return services;
      }
   }
}
