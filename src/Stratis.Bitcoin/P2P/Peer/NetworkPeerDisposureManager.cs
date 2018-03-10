using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerDisposureManager : IDisposable
    {
        private readonly CancellationTokenSource cancellaltion;
        private readonly List<Task> disposureTasks;
        private readonly object mutex;
        
        public NetworkPeerDisposureManager()
        {
            this.cancellaltion = new CancellationTokenSource();
            this.disposureTasks = new List<Task>();
            this.mutex = new object();
        }

        /// <summary>
        /// Disposes the peer in a separate task.
        /// </summary>
        /// <param name="peer">Peer to be disposed.</param>
        /// <param name="onPeerDisposed">Callback which is called when peer disposure is completed.</param>
        public void DisposePeer(INetworkPeer peer, Action onPeerDisposed = null)
        {
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
                                List<Task> completedTasks = this.disposureTasks.Where(x => x.IsCompleted).ToList();

                                foreach (Task completedTask in completedTasks)
                                    this.disposureTasks.Remove(completedTask);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                });

                this.disposureTasks.Add(disposureTask);
            }
        }
        
        /// <inheritdoc />
        public void Dispose()
        {
            lock (this.mutex)
            {
                this.cancellaltion.Cancel();

                if (this.disposureTasks.Count != 0)
                    Task.WhenAll(this.disposureTasks).GetAwaiter().GetResult();

                this.disposureTasks.Clear();
            }
        }
    }
}
