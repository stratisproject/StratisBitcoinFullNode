using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>Disposes peer in a separated task.</summary>
    public class NetworkPeerDisposureManager : IDisposable
    {
        /// <summary>Cancellation source.</summary>
        private readonly CancellationTokenSource cancellaltion;

        /// <summary>List of tasks that dispose peers.</summary>
        private readonly List<Task> disposureTasks;

        /// <summary>Protects access to <see cref="disposureTasks"/></summary>
        private readonly object mutex;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public NetworkPeerDisposureManager(ILoggerFactory loggerFactory)
        {
            this.cancellaltion = new CancellationTokenSource();
            this.disposureTasks = new List<Task>();
            this.mutex = new object();

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Disposes the peer in a separate task.
        /// </summary>
        /// <param name="peer">Peer to be disposed.</param>
        /// <param name="onPeerDisposed">Callback which is called when peer disposure is completed.</param>
        public void DisposePeer(INetworkPeer peer, Action onPeerDisposed = null)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peer), peer.RemoteSocketAddress);

            lock (this.mutex)
            {
                Task disposureTask = Task.Run(() =>
                {
                    try
                    {
                        peer.Dispose();
                        onPeerDisposed?.Invoke();

                        // Delete tasks that were completed before from the list.
                        if (!this.cancellaltion.IsCancellationRequested)
                        {
                            lock (this.mutex)
                            {
                                this.disposureTasks.RemoveAll(x => x.IsCompleted);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.logger.LogError("Error while disposing a peer occurred: {0}", e.ToString());
                    }
                });

                this.disposureTasks.Add(disposureTask);
            }

            this.logger.LogTrace("(-)");
        }
        
        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            lock (this.mutex)
            {
                this.cancellaltion.Cancel();

                if (this.disposureTasks.Count != 0)
                    Task.WhenAll(this.disposureTasks).GetAwaiter().GetResult();

                this.disposureTasks.Clear();
            }

            this.logger.LogTrace("(-)");
        }
    }
}
