using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.PlatformAbstractions;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IFullNode"/>
   /// </summary>
   public class FullNodeBuilder : IFullNodeBuilder {
      private readonly List<Action<IServiceCollection>> _configureServicesDelegates;

      private bool _fullNodeBuilt;

      /// <summary>
      /// Initializes a new instance of the <see cref="FullNodeBuilder"/> class.
      /// </summary>
      public FullNodeBuilder() {
         _configureServicesDelegates = new List<Action<IServiceCollection>>();
      }


      /// <summary>
      /// Adds a delegate for configuring additional services for the host or web application. This may be called
      /// multiple times.
      /// </summary>
      /// <param name="configureServices">A delegate for configuring the <see cref="IServiceCollection"/>.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices) {
         if (configureServices == null) {
            throw new ArgumentNullException(nameof(configureServices));
         }

         _configureServicesDelegates.Add(configureServices);
         return this;
      }

      /// <summary>
      /// Builds the required features and an <see cref="IFullNode"/> which orchestrate them.
      /// </summary>
      public IFullNode Build(NodeArgs nodeSettings) {
         if (_fullNodeBuilt) {
            //ref. to use localized exceptions
            //throw new InvalidOperationException(Resources.WebHostBuilder_SingleInstance);
            throw new InvalidOperationException("full node already built");
         }
         _fullNodeBuilt = true;


         var fullNodeServices = BuildCommonServices();

         var applicationServices = fullNodeServices.Clone();
         var fullNodeServiceProvider = fullNodeServices.BuildServiceProvider();

         var fullNode = new FullNode(nodeSettings);
         fullNode.InitializeServiceLayer(applicationServices, fullNodeServiceProvider);

         return fullNode;
      }

      private IServiceCollection BuildCommonServices() {
         var services = new ServiceCollection();

         foreach (var configureServices in _configureServicesDelegates) {
            configureServices(services);
         }

         return services;
      }
   }
}