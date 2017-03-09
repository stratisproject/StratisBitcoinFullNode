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
      private readonly IServiceCollection _applicationServiceCollection;
      private IStartup _startup;
      private ApplicationLifetime _applicationLifetime;
      private FullNodeServiceExecutor _fullNodeServiceExecutor;

      private readonly IServiceProvider _fullNodeServiceProvider;
      private readonly FullNodeOptions _options;
      private readonly IConfiguration _config;

      private IServiceProvider _applicationServices;
      private ILogger<FullNode> _logger;


      public IServiceProvider Services {
         get {
            EnsureApplicationServices();
            return _applicationServices;
         }
      }




      public FullNode(IServiceCollection appServices, IServiceProvider fullNodeServiceProvider, FullNodeOptions options, IConfiguration config) {
         if (appServices == null) {
            throw new ArgumentNullException(nameof(appServices));
         }

         if (fullNodeServiceProvider == null) {
            throw new ArgumentNullException(nameof(fullNodeServiceProvider));
         }

         if (config == null) {
            throw new ArgumentNullException(nameof(config));
         }

         _config = config;
         _options = options;
         _applicationServiceCollection = appServices;
         _fullNodeServiceProvider = fullNodeServiceProvider;
         _applicationServiceCollection.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
         _applicationServiceCollection.AddSingleton<FullNodeServiceExecutor>();
      }


      public void Initialize() {
         //if (_application == null) {
         //   _application = BuildApplication();
         //}
      }



      private void DisposeBuilderStuff() {
         //_logger?.Shutdown();
         // Fire IApplicationLifetime.Stopping
         _applicationLifetime?.StopApplication();
         // Fire the IHostedService.Stop
         _fullNodeServiceExecutor?.Stop();
         (_fullNodeServiceProvider as IDisposable)?.Dispose();
         (_applicationServices as IDisposable)?.Dispose();
         // Fire IApplicationLifetime.Stopped
         _applicationLifetime?.NotifyStopped();
         //HostingEventSource.Log.HostStop();
      }


      private void EnsureApplicationServices() {
         if (_applicationServices == null) {
            EnsureStartup();
            _applicationServices = _startup.ConfigureServices(_applicationServiceCollection);
         }
      }

      private void EnsureStartup() {
         //if (_startup != null) {
         //   return;
         //}

         //_startup = _fullNodeServiceProvider.GetRequiredService<IStartup>();
      }
   }
}