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
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.RequiredNodeComponents;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IFullNode"/>
   /// </summary>
   public class FullNodeBuilder : IFullNodeBuilder {
      private readonly List<Action<IServiceCollection>> _configureServicesDelegates;
      private readonly List<Action<IServiceProvider>> _configureDelegates;

      private bool _fullNodeBuilt;

      /// <summary>
      /// Initializes a new instance of the <see cref="FullNodeBuilder"/> class.
      /// </summary>
      public FullNodeBuilder() {
         _configureServicesDelegates = new List<Action<IServiceCollection>>();
         //_configureServicesDelegates.Add(services => {
         //   //add required node features
         //   services.AddRequiredNodeFeature();
         //});

         _configureServicesDelegates.Add(services => {
            services.AddRequiredComponentsFeature();
         });

         _configureDelegates = new List<Action<IServiceProvider>>();
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
      /// Specify the delegate that is used to configure one of the registered services.
      /// This delegate should be used to configure a service once it has been registered within a ConfigureService action
      /// </summary>
      /// <param name="configure">The delegate that configures registered services</param>
      /// <returns></returns>
      public IFullNodeBuilder Configure(Action<IServiceProvider> configure) {
         if (configure == null) {
            throw new ArgumentNullException(nameof(configure));
         }

         _configureDelegates.Add(configure);
         return this;
      }



      /// <summary>
      /// Builds the required features and an <see cref="IFullNode"/> which orchestrate them.
      /// </summary>
      public IFullNode Build() {
         if (_fullNodeBuilt) {
            //ref. to use localized exceptions
            //throw new InvalidOperationException(Resources.WebHostBuilder_SingleInstance);
            throw new InvalidOperationException("full node already built");
         }
         _fullNodeBuilt = true;


         var fullNodeServices = BuildServices();
         var fullNodeServiceProvider = fullNodeServices.BuildServiceProvider();
         ConfigureServices(fullNodeServiceProvider);

         //obtain the nodeArgs from the service (it's set used FullNodeBuilder.UseNodeArgs)
         var nodeArgs = fullNodeServiceProvider.GetService<NodeArgs>();
         if (nodeArgs == null) {
            Logs.FullNode?.LogWarning("Using default NodeArgs");
            nodeArgs = NodeArgs.Default();
         }

         var fullNode = new FullNode(fullNodeServices, nodeArgs);

         return fullNode;
      }

      private IServiceCollection BuildServices() {
         var services = new ServiceCollection();

         //register services
         foreach (var configureServices in _configureServicesDelegates) {
            configureServices(services);
         }

         return services;
      }


      private void ConfigureServices(IServiceProvider serviceProvider) {
         //configure registered services
         foreach (var configure in _configureDelegates) {
            configure(serviceProvider);
         }
      }
   }
}