using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRService : ISignalRService
    {
        private IWebHost webHost;
        private readonly ILogger logger;
        private bool started;
        private readonly BehaviorSubject<string> startedStream = new BehaviorSubject<string>(string.Empty);
        private readonly object lockObject = new object();
        private AsyncQueue<(string topic, string data)> messageQueue;
        private readonly ReplaySubject<(string topic, string data)> messageStream = new ReplaySubject<(string topic, string data)>(1);

        public SignalRService(SignalRSettings settings, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.StartedStream = this.startedStream.AsObservable();

            this.messageQueue = new AsyncQueue<(string topic, string data)>((message, _) =>
            {
                this.messageStream.OnNext(message);
                return Task.CompletedTask;
            });

            this.MessageStream = this.messageStream.AsObservable();
            this.Address = new Uri($"http://localhost:{settings.Port}");
            this.HubRoute = new Uri($"{this.Address.AbsoluteUri}{settings.HubRoute}");
        }

        /// <summary><see cref="ISignalRService.Address" /></summary>
        public Uri Address { get; }

        /// <summary><see cref="ISignalRService.HubRoute" /></summary>
        public Uri HubRoute { get; }

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

        /// <inheritdoc />
        public IObservable<string> StartedStream { get; }

        /// <summary><see cref="ISignalRService.StartAsync" /></summary>
        public async Task<bool> StartAsync()
        {
            var started = await Task.Run(() =>
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
                    return true;
                }
                catch (Exception e)
                {
                    this.logger.LogCritical("Failed to host at {0}: {1}", address, e.Message);
                    return false;
                }
            });

            return this.Started = started;
        }

        public void Dispose()
        {
            this.messageStream.OnCompleted();
            this.startedStream.OnCompleted();

            this.messageStream.Dispose();
            this.startedStream.Dispose();

            this.messageQueue?.Dispose();
            this.messageQueue = null;
            this.webHost?.Dispose();
            this.webHost = null;
        }

        public bool Started
        {
            get { lock (this.lockObject) return this.started; }

            private set
            {
                lock (this.lockObject)
                {
                    if (this.started == value) return;
                    this.started = value;
                }

                if (value) this.startedStream.OnNext(this.HubRoute.AbsoluteUri);
            }
        }
    }
}