using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.SignalR
{
    public class SignalRService : IDisposable, ISignalRService
    {
        private IWebHost webHost;
        private readonly ILogger logger;
        private readonly Subject<(string topic, string data)> messageStream = new Subject<(string topic, string data)>();

        public SignalRService(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.MessageStream = this.messageStream.AsObservable();

            this.CreateAddress();
        }

        public Uri Address { get; private set; }

        public IObservable<(string topic, string data)> MessageStream { get; }

        public Task PublishAsync(string topic, string data) =>
            Task.Run(() => this.messageStream.OnNext((topic, data)));

        public Task<bool> StartAsync()
        {
            return Task.Run(() =>
            {
                var address = this.Address.AbsoluteUri;
                try
                {
                    // This allows injection of this service instance into the SignalR-generated hub.
                    var serviceDescriptor = new ServiceDescriptor(typeof(ISignalRService), this);

                    this.webHost = new WebHostBuilder()
                        .ConfigureServices(x => x.Add(serviceDescriptor))
                        .UseKestrel()
                        .UseIISIntegration()
                        .UseUrls(address)
                        .UseStartup<Startup>()
                        .Build();

                    this.webHost.Start();

                    this.logger.LogInformation($"Hosted at {0}", address);
                    return true;
                }
                catch (Exception e)
                {
                    this.logger.LogCritical($"Failed to host at {0}: {1}", address, e.Message);
                    return false;
                }
            });
        }

        public void Dispose()
        {
            this.webHost?.Dispose();
            this.webHost = null;
        }

        /// <summary> Sets Address to a unique localhost address as used by this server and clients.
        /// Address is available to the client via the SignalRController. </summary>
        private void CreateAddress()
        {
            int[] nextFreePort = { 0 };
            Utilities.IpHelper.FindPorts(nextFreePort);
            nextFreePort[0] = 8080; // <- 8080 is hardcoded for development.  If you see this, you shouldn't be!
            this.Address = new Uri($"http://localhost:{nextFreePort.First()}");
        }
    }
}