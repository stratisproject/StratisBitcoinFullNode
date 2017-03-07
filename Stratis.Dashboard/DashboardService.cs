using Microsoft.AspNetCore.Hosting;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Logging;
using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Stratis.Dashboard.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Dashboard {
   public class DashboardService {
      public class Configuration {
         public string ContentRoot { get; set; } = Directory.GetCurrentDirectory();

         internal Configuration() {

         }
      }

      IWebHost _host;
      Configuration _configuration;

      public FullNode FullNode { get; private set; }


      public DashboardService() {
         this._configuration = new Dashboard.DashboardService.Configuration();
      }

      public DashboardService(Action<Configuration> configureAction) : this() {
         configureAction?.Invoke(this._configuration);
      }


      public bool AttachNode(FullNode fullNode) {

         this.FullNode = fullNode;

         //needs a proper configuration parameter, now reusing RPC
         return fullNode.Args.RPC != null;
      }

      public bool Start() {
         if (_host == null) {
            //todo need a settings for this
            int port = 5000;

            try {
               var hostBuilder = new WebHostBuilder()
                 .UseKestrel();

               if (_configuration?.ContentRoot != null) {
                  hostBuilder.UseContentRoot(_configuration?.ContentRoot);
               }

               hostBuilder.UseUrls($"http://localhost:{port}")
                 .UseStartup<Startup>()
                 .ConfigureServices(services => {
                    services.AddSingleton<IFullNodeGetter, FullNodeGetter>(provider => new FullNodeGetter(FullNode));
                 });


               _host = hostBuilder.Build();

               _host.Start();

               Logs.FullNode.LogInformation($"WebWallet STARTED on port {port}");

               return true;
            }
            catch (Exception ex) {
               Logs.FullNode.LogError($"Error starting WebWallet on port {port}: {ex.Message}");
               return false;
            }
         }
         else {
            Logs.FullNode.LogWarning($"WebWallet already started");
            return false;
         }
      }

      public bool Stop() {
         if (_host != null) {
            //can Kestrel be stopped?
         }

         return false;
      }
   }
}
