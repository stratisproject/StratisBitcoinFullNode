using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Stratis.Bitcoin.RPC;
using NBitcoin;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Consensus;
using NBitcoin.Protocol;
using Microsoft.AspNetCore.Hosting.Internal;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.BlockPulling;
using System.Text;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Miner;
using Microsoft.Extensions.Configuration;

namespace Stratis.Bitcoin {

   public partial class FullNode : IFullNode {
      private IServiceCollection _applicationServiceCollection;
      private ApplicationLifetime _applicationLifetime;
      private FullNodeFeatureExecutor _fullNodeFeatureExecutor;

      private IServiceProvider _fullNodeServiceProvider;

      private IServiceProvider _applicationServices;
      private ILogger<FullNode> _logger;


      public IServiceProvider Services { get { return _applicationServices; } }


      /// <summary>
      /// internal implementation that wire the services to the FullNode instance
      /// </summary>
      /// <param name="appServices"></param>
      /// <param name="fullNodeServiceProvider"></param>
      internal void InitializeServiceLayer(IServiceCollection appServices, IServiceProvider fullNodeServiceProvider) {
         if (appServices == null) {
            throw new ArgumentNullException(nameof(appServices));
         }

         if (fullNodeServiceProvider == null) {
            throw new ArgumentNullException(nameof(fullNodeServiceProvider));
         }

         _applicationServiceCollection = appServices;
         _fullNodeServiceProvider = fullNodeServiceProvider;
         _applicationServiceCollection.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
         _applicationServiceCollection.AddSingleton<FullNodeFeatureExecutor>();
      }



      /// <summary>
      /// temporary method that should merged into the Start() method
      /// </summary>
      protected void StartBuilderStuff() {
         _applicationServices = _applicationServiceCollection.BuildServiceProvider();

         _applicationLifetime = _applicationServices?.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
         _fullNodeFeatureExecutor = _applicationServices?.GetRequiredService<FullNodeFeatureExecutor>();

         // Fire IApplicationLifetime.Started
         _applicationLifetime?.NotifyStarted();

         //start all registered features
         _fullNodeFeatureExecutor?.Start(this);
      }



      /// <summary>
      /// temporary method that should merged into the Dispose method
      /// </summary>
      protected void DisposeBuilderStuff() {
         // Fire IApplicationLifetime.Stopping
         _applicationLifetime?.StopApplication();
         // Fire the IHostedService.Stop
         _fullNodeFeatureExecutor?.Stop();
         (_fullNodeServiceProvider as IDisposable)?.Dispose();
         (_applicationServices as IDisposable)?.Dispose();
         // Fire IApplicationLifetime.Stopped
         _applicationLifetime?.NotifyStopped();
      }


      /// <summary>
      /// updates the _applicationServices from the services list
      /// </summary>
      public void UpdateApplicationServiceProvider() {
         _applicationServices = _applicationServiceCollection.BuildServiceProvider();
      }
   }
}