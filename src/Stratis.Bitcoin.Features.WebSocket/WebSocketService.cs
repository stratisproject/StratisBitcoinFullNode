using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketService : IWebSocketService
    {
        private readonly object lockObject = new object();

        private readonly ILogger logger;

        private readonly bool started;

        private readonly IServiceProvider serviceProvider;

        public WebSocketService(WebSocketSettings settings, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);

            this.serviceProvider = serviceProvider;
        }

        public static IWebHostBuilder CreateWebHostBuilder(FullNode fullNode, IEnumerable<ServiceDescriptor> services, IWebSocketService webSocketService, string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .ConfigureServices(collection =>
                {
                    // copies all the services defined for the full node to the Api.
                    // also copies over singleton instances already defined
                    foreach (ServiceDescriptor service in services)
                    {
                        object obj = fullNode.Services.ServiceProvider.GetService(service.ServiceType);
                        if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
                        {
                            collection.AddSingleton(service.ServiceType, obj);
                        }
                        else
                        {
                            collection.Add(service);
                        }
                    }

                    collection.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(IWebSocketService), webSocketService));
                })
                .UseStartup<Startup>().UseUrls("http://localhost:4336");
        }

        /// <summary><see cref="ISignalRService.StartAsync" /></summary>
        public async Task<bool> StartAsync(FullNode fullNode, IEnumerable<ServiceDescriptor> services)
        {
            var started = await Task.Run(() =>
            {
                var args = new string[] { };

                CreateWebHostBuilder(fullNode, services, this, args).Build().Start(); //.Run();

                return true;

                //var address = this.Address.AbsoluteUri;
                //try
                //{
                //    // This allows injection of this service into the instance of the SignalR-generated hub.
                //    var signalRServiceDescriptor = new ServiceDescriptor(typeof(ISignalRService), this);
                //    this.webHost = new WebHostBuilder()
                //       .ConfigureServices(x => x.Add(signalRServiceDescriptor))
                //       .UseKestrel()
                //       .UseIISIntegration()
                //       .UseUrls(address)
                //       .UseStartup<Startup>()
                //       .Build();
                //    this.webHost.Start();
                //    this.logger.LogInformation("Hosted at {0}", address);
                //    return true;
                //}
                //catch (Exception e)
                //{
                //    this.logger.LogCritical("Failed to host at {0}: {1}", address, e.Message);
                //    return false;
                //}
            });

            return started;

            //return this.Started = started;
        }

        //public bool Started
        //{
        //    get { lock (this.lockObject) return this.started; }
        //    private set
        //    {
        //        lock (this.lockObject)
        //        {
        //            if (this.started == value) return;
        //            this.started = value;
        //        }

        //        if (value) this.startedStream.OnNext(this.HubRoute.AbsoluteUri);
        //    }
        //}

        public void Dispose()
        {
            //this.messageStream.OnCompleted();
            //this.startedStream.OnCompleted();
            //this.messageStream.Dispose();
            //this.startedStream.Dispose();
            //this.messageQueue?.Dispose();
            //this.messageQueue = null;
            //this.webHost?.Dispose();
            //this.webHost = null;
        }

        public async Task Broadcast(string message)
        {
            if (Startup.Provider == null)
            {
                return;
            }

            IHubContext<FullNodeHub> hubContext = Startup.Provider.GetService<IHubContext<FullNodeHub>>();

            if (hubContext == null)
            {
                return;
            }

            await hubContext.Clients.All.SendAsync("BroadcastMessage", "daemon", message);
        }
    }
}
