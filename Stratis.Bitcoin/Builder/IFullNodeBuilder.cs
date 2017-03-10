using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IWebHost"/>.
   /// </summary>
   public interface IFullNodeBuilder {
      /// <summary>
      /// Builds an <see cref="IFullNode"/>.
      /// </summary>
      /// <param name="nodeSettings"></param>
      IFullNode Build(NodeArgs nodeSettings);

      /// <summary>
      /// Specify the delegate that is used to configure the services of the full node.
      /// </summary>
      /// <param name="configureServices">The delegate that configures the <see cref="IServiceCollection"/>.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices);
   }
}