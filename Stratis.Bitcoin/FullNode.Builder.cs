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
      private IServiceCollection _fullNodeServiceServiceCollection;
      private ApplicationLifetime _applicationLifetime;
      private FullNodeFeatureExecutor _fullNodeFeatureExecutor;

      private IServiceProvider _fullNodeServices;

      private ILogger<FullNode> _logger;


      public IServiceProvider Services { get { return _fullNodeServices; } }





      /// <summary>
      /// internal implementation that wire the services to the FullNode instance
      /// </summary>
      /// <param name="appServices"></param>
      /// <param name="fullNodeServiceProvider"></param>
      internal FullNode(IServiceCollection nodeServices, NodeArgs nodeArgs) : this(nodeArgs) {
         if (nodeServices == null) {
            throw new ArgumentNullException(nameof(nodeServices));
         }

         if (nodeArgs == null) {
            throw new ArgumentNullException(nameof(nodeArgs));
         }

         _fullNodeServiceServiceCollection = nodeServices;
         _fullNodeServiceServiceCollection.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
         _fullNodeServiceServiceCollection.AddSingleton<FullNodeFeatureExecutor>();


         //espose needed components as Services using factories that are invoked after they are created in the RequiredFeatures 
         _fullNodeServiceServiceCollection.AddSingleton(services => this.Chain);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.ConnectionManager);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.CoinView);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.ConsensusLoop.Validator);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.DateTimeProvider);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.ChainBehaviorState);
         _fullNodeServiceServiceCollection.AddSingleton(services => this.GlobalCancellation);


         _fullNodeServices = _fullNodeServiceServiceCollection.BuildServiceProvider();
      }



      /// <summary>
      /// temporary method that should merged into the Start() method
      /// </summary>
      protected void StartBuilderStuff() {
         _applicationLifetime = _fullNodeServices?.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;
         _fullNodeFeatureExecutor = _fullNodeServices?.GetRequiredService<FullNodeFeatureExecutor>();

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
         _fullNodeFeatureExecutor?.Stop(this);
         (_fullNodeServices as IDisposable)?.Dispose();
         (_fullNodeServices as IDisposable)?.Dispose();
         // Fire IApplicationLifetime.Stopped
         _applicationLifetime?.NotifyStopped();
      }


      /// <summary>
      /// updates the _applicationServices from the services list
      /// </summary>
      public void UpdateApplicationServiceProvider() {
         _fullNodeServices = _fullNodeServiceServiceCollection.BuildServiceProvider();
      }
   }
}