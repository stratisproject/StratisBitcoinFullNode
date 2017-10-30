using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class CustomThreadPoolTaskScheduler : TaskScheduler, IDisposable
    {
        int _ThreadCount;
        public CustomThreadPoolTaskScheduler(int threadCount, int maxQueued, string name = null)
        {
            _ThreadCount = threadCount;
            _Tasks = new BlockingCollection<Task>(new ConcurrentQueue<Task>(), maxQueued);
            _AvailableThreads = threadCount;
            for (int i = 0 ; i < threadCount ; i++)
            {
                new Thread(Do)
                {
                    IsBackground = true,
                    Name = name
                }.Start();
            }
        }

        public override int MaximumConcurrencyLevel
        {
            get
            {
                return _ThreadCount;
            }
        }

        CancellationTokenSource _Cancel = new CancellationTokenSource();
        void Do(object state)
        {
            try
            {
                foreach (var task in _Tasks.GetConsumingEnumerable(_Cancel.Token))
                {
                    Interlocked.Decrement(ref _AvailableThreads);
                    TryExecuteTask(task);
                    Interlocked.Increment(ref _AvailableThreads);
                    if (RemainingTasks == 0)
                        _Finished.Set();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        public int QueuedCount
        {
            get
            {
                return _Tasks.Count;
            }
        }

        int _AvailableThreads;
        public int AvailableThreads
        {
            get
            {
                return _AvailableThreads;
            }
        }

        public int RemainingTasks
        {
            get
            {
                return (_ThreadCount - AvailableThreads) + QueuedCount;
            }
        }

        public int ThreadsCount
        {
            get
            {
                return _ThreadCount;
            }
        }

        BlockingCollection<Task> _Tasks;
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _Tasks;
        }

        protected override void QueueTask(Task task)
        {
            AssertNotDisposed();
            _Tasks.Add(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            AssertNotDisposed();
            return false;
        }

        #region IDisposable Members

        bool _disposed;
        public void Dispose()
        {
            _disposed = true;
            _Cancel.Cancel();
        }

        #endregion

        AutoResetEvent _Finished = new AutoResetEvent(false);
        public void WaitFinished()
        {
            AssertNotDisposed();
            while (true)
            {
                if (_disposed)
                    return;
                if (RemainingTasks == 0)
                    return;
                _Finished.WaitOne(1000);
            }
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("CustomThreadPoolTaskScheduler");
        }
    }
}
