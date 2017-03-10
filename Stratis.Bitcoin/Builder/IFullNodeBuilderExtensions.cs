using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin {
   public static class IFullNodeBuilderExtensions {
      /// <summary>
      /// Set whether startup errors should be captured in the configuration settings of the web host.
      /// When enabled, startup exceptions will be caught and an error page will be returned. If disabled, startup exceptions will be propagated.
      /// </summary>
      /// <param name="builder">The <see cref="IFullNodeBuilder"/> to configure.</param>
      /// <param name="nodeArgs">the NodeArgs to use to configure the FullNode</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public static IFullNodeBuilder UseNodeArgs(this IFullNodeBuilder builder, NodeArgs nodeArgs) {
         return builder.ConfigureServices(service => {
            service.AddSingleton(nodeArgs);
         });
      }
   }
}