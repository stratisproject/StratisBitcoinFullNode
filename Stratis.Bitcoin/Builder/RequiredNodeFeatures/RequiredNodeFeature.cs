using Stratis.Bitcoin.MemoryPool;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using System;
using Stratis.Bitcoin.Configuration;
using System.IO;
using Stratis.Bitcoin.Logging;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder.RequiredNodeComponents;

namespace Stratis.Bitcoin.Builder {
   public static class RequiredNodeFeature {

      internal static void AddRequiredNodeFeature(this IServiceCollection services) {

         services
          .AddAddressManagerService()
          .AddChainService();

         //services.AddSingleton(nodeInstance.ConnectionManager);
         //services.AddSingleton(nodeInstance.CoinView);
         //services.AddSingleton(nodeInstance.ConsensusLoop.Validator);
         //services.AddSingleton(nodeInstance.DateTimeProvider);
         //services.AddSingleton(nodeInstance.ChainBehaviorState);
         //services.AddSingleton(nodeInstance.GlobalCancellation);
      }
   }
}
