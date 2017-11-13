using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Protocol
{
    public interface IMessageListener<in T>
    {
        void PushMessage(T message);
    }

    public class NullMessageListener<T> : IMessageListener<T>
    {
        public void PushMessage(T message)
        {
        }
    }

    public class NewThreadMessageListener<T> : IMessageListener<T>
    {
        private readonly Action<T> process;
        public NewThreadMessageListener(Action<T> process)
        {
            this.process = process ?? throw new ArgumentNullException("process");
        }

        public void PushMessage(T message)
        {
            if (message != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        this.process(message);
                    }
                    catch (Exception ex)
                    {
                        NodeServerTrace.Error("Unexpected expected during message loop", ex);
                    }
                });
            }
        }
    }

    public class EventLoopMessageListener<T> : IMessageListener<T>, IDisposable
    {
        private BlockingCollection<T> messageQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
        public BlockingCollection<T> MessageQueue { get { return this.messageQueue; } }

        private CancellationTokenSource cancellationSource = new CancellationTokenSource();

        public EventLoopMessageListener(Action<T> processMessage)
        {
            new Thread(new ThreadStart(() =>
            {
                try
                {
                    while (!this.cancellationSource.IsCancellationRequested)
                    {
                        T message = this.messageQueue.Take(this.cancellationSource.Token);
                        if (message != null)
                        {
                            try
                            {
                                processMessage(message);
                            }
                            catch (Exception ex)
                            {
                                NodeServerTrace.Error("Unexpected expected during message loop", ex);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            })).Start();
        }

        public void PushMessage(T message)
        {
            this.messageQueue.Add(message);
        }

        public void Dispose()
        {
            if (this.cancellationSource.IsCancellationRequested)
                return;

            this.cancellationSource.Cancel();
            this.cancellationSource.Dispose();
        }
    }

    public class PollMessageListener<T> : IMessageListener<T>
    {
        private BlockingCollection<T> messageQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
        public BlockingCollection<T> MessageQueue { get { return this.messageQueue; } }

        public virtual T ReceiveMessage(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.MessageQueue.Take(cancellationToken);
        }

        public virtual void PushMessage(T message)
        {
            this.messageQueue.Add(message);
        }
    }
}