using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin.Builder.Extensions {
   public static class MempoolModuleFullNodeBuilderExtension {
      public static IFullNodeBuilder UseMempool(this IFullNodeBuilder fullNodeBuilder) {
         fullNodeBuilder.ConfigureServices((services) => {

            services.AddSingleton<IFullNodeFeature>(new MempoolFeature());

            MempoolFeature.RegisterNeededServices(services);

         });

         return fullNodeBuilder;
      }
   }
}
