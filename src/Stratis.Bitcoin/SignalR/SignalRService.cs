using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.SignalR
{
    public class SignalRService : IDisposable, ISignalRService
    {
        private IWebHost webHost;
        private readonly ILogger logger;
        private bool started;
        private readonly object lockObject = new object();
        private AsyncQueue<(string topic, string data)> messageQueue;
        private readonly ReplaySubject<(string topic, string data)> messageStream = new ReplaySubject<(string topic, string data)>(1);

        public SignalRService(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.messageQueue = new AsyncQueue<(string topic, string data)>((message, _) =>
            {
                this.messageStream.OnNext(message);
                return Task.CompletedTask;
            });

            this.MessageStream = this.messageStream.AsObservable();

            this.Address = new Uri($"http://localhost:{IpHelper.NextFreePort}");
        }

        /// <summary><see cref="ISignalRService.Address" /></summary>
        public Uri Address { get; }

        /// <summary><see cref="ISignalRService.MessageStream" /></summary>
        public IObservable<(string topic, string data)> MessageStream { get; }

        /// <summary><see cref="ISignalRService.SendAsync" /></summary>
        public Task<bool> SendAsync(string topic, string data)
        {
            if (!this.Started)
            {
                this.logger.LogWarning("Service not started, so message not dispatched");
                return Task.FromResult(false);
            }

            this.messageQueue.Enqueue((topic, data));
            return Task.FromResult(true);
        }

        /// <summary><see cref="ISignalRService.StartAsync" /></summary>
        public Task<bool> StartAsync()
        {
            return Task.Run(() =>
            {
                var address = this.Address.AbsoluteUri;
                try
                {
                    // This allows injection of this service into the instance of the SignalR-generated hub.
                    var signalRServiceDescriptor = new ServiceDescriptor(typeof(ISignalRService), this);

                    this.webHost = new WebHostBuilder()
                        .ConfigureServices(x => x.Add(signalRServiceDescriptor))
                        .UseKestrel()
                        .UseIISIntegration()
                        .UseUrls(address)
                        .UseStartup<Startup>()
                        .Build();

                    this.webHost.Start();

                    this.logger.LogInformation("Hosted at {0}", address);
                    return this.Started = true;
                }
                catch (Exception e)
                {
                    this.logger.LogCritical("Failed to host at {0}: {1}", address, e.Message);
                    return false;
                }
            });
        }

        public void Dispose()
        {
            this.messageQueue?.Dispose();
            this.messageQueue = null;
            this.webHost?.Dispose();
            this.webHost = null;
        }

        private bool Started
        {
            get { lock (this.lockObject) return this.started; }
            set { lock (this.lockObject) this.started = value; }
        }
    }
}