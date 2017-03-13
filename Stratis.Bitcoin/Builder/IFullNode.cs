using System;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

namespace Stratis.Bitcoin {
   /// <summary>
   /// Represents a configured web host.
   /// </summary>
   public interface IFullNode : IDisposable {
      ///// <summary>
      ///// The <see cref="IFeatureCollection"/> exposed by the configured server.
      ///// </summary>
      //IFeatureCollection ServerFeatures { get; }

      /// <summary>
      /// The <see cref="IServiceProvider"/> for the host.
      /// </summary>
      IServiceProvider Services { get; }

      /// <summary>
      /// Starts listening on the configured addresses.
      /// </summary>
      void Start();
   }
}