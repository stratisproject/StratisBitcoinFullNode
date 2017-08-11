using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using DBreeze;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Exclusive session executed in a single thread to access the DBreeze database.
    /// </summary>
    public interface IDBreezeSingleThreadSession : IDisposable
    {
        /// <summary>Database transaction that is used for accessing the database.</summary>
        DBreeze.Transactions.Transaction Transaction { get; }

        /// <summary>
        /// Executes a caller defined method to be executed using the exclusive task scheduler, which allows the method to access the database safely.
        /// </summary>
        /// <param name="action">Method to execute using the exclusive task scheduler that can access the database safely.</param>
        Task Execute(Action action);

        /// <summary>
        /// Executes a caller defined method to be executed using the exclusive task scheduler, which allows the method to access the database safely.
        /// </summary>
        /// <typeparam name="T">Return type of the delegated method.</typeparam>
        /// <param name="action">Method to execute using the exclusive task scheduler that can access the database safely.</param>
        /// <returns>Return value of the delegated method.</returns>
        Task<T> Execute<T>(Func<T> action);
    }

    /// <inheritdoc />
    public class DBreezeSingleThreadSession : IDBreezeSingleThreadSession
    {
        /// <summary>Access to DBreeze database.</summary>
        protected DBreezeEngine engine;

        /// <summary>
        /// Scheduler that uses only a single thread to provide ability for exclusive execution of the tasks. 
        /// </summary>
        /// <remarks>
        /// This is used as an alternative to lock-based critical section implementation, but allegedly DBreeze 
        /// does require a dedicated thread to access it and does not (did not?) support multithreading access.
        /// </remarks>
        private CustomThreadPoolTaskScheduler singleThread;

        /// <summary>Database transaction that is used for accessing the database.</summary>
        /// <remarks>As we access the database in the exclusive thread, a single transaction is enough for all use cases.</remarks>
        private DBreeze.Transactions.Transaction transaction;
        /// <inheritdoc />
        public DBreeze.Transactions.Transaction Transaction
        {
            get
            {
                return this.transaction;
            }
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// <para>It creates an exclusive scheduler for tasks accessing database and initializes the database engine on disk.</para>
        /// </summary>
        /// <param name="threadName">Name of the exclusive thread that will care about tasks accessing the database in exclusive manner.</param>
        /// <param name="folder">Path to the directory to hold DBreeze database files.</param>
        public DBreezeSingleThreadSession(string threadName, string folder)
        {
            Guard.NotEmpty(threadName, nameof(threadName));
            Guard.NotEmpty(folder, nameof(folder));

            this.singleThread = new CustomThreadPoolTaskScheduler(1, 100, threadName);
            new Task(() =>
            {
                DBreeze.Utils.CustomSerializator.ByteArraySerializator = NBitcoinSerialize;
                DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = NBitcoinDeserialize;
                this.engine = new DBreezeEngine(folder);
                this.transaction = this.engine.GetTransaction();
            }).Start(this.singleThread);
        }

        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
		internal static byte[] NBitcoinSerialize(object obj)
        {
            IBitcoinSerializable serializable = obj as IBitcoinSerializable;
            if (serializable != null)
                return serializable.ToBytes();

            uint256 u256 = obj as uint256;
            if (u256 != null)
                return u256.ToBytes();
            uint160 u160 = obj as uint160;
            if (u160 != null)
                return u160.ToBytes();
            uint? u32 = obj as uint?;
            if (u32 != null)
                return u32.ToBytes();

            object[] a = obj as object[];
            if (a != null)
            {
                var result = (from x in a select NBitcoinSerialize(x)).ToArray().SelectMany(x => x).ToArray();
                return result;
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
		internal static object NBitcoinDeserialize(byte[] bytes, Type type)
        {
            if (type == typeof(Coins))
            {
                Coins coin = new Coins();
                coin.ReadWrite(bytes);
                return coin;
            }

            if (type == typeof(BlockHeader))
            {
                BlockHeader header = new BlockHeader();
                header.ReadWrite(bytes);
                return header;
            }

            if (type == typeof(RewindData))
            {
                RewindData rewind = new RewindData();
                rewind.ReadWrite(bytes);
                return rewind;
            }

            if (type == typeof(uint256))
            {
                return new uint256(bytes);
            }

            if (type == typeof(Block))
            {
                return new Block(bytes);
            }

            if (type == typeof(BlockStake))
            {
                return new BlockStake(bytes);
            }

            throw new NotSupportedException();
        }

        /// <inheritdoc />
		public Task Execute(Action act)
        {
            Guard.NotNull(act, nameof(act));

            this.AssertNotDisposed();
            var task = new Task(() =>
            {
                this.AssertNotDisposed();
                act();
            });
            task.Start(this.singleThread);
            return task;
        }

        /// <inheritdoc />
		public Task<T> Execute<T>(Func<T> act)
        {
            Guard.NotNull(act, nameof(act));

            this.AssertNotDisposed();
            var task = new Task<T>(() =>
            {
                this.AssertNotDisposed();
                return act();
            });
            task.Start(this.singleThread);
            return task;
        }

        /// <summary>
        /// Throws exception if the instance of the object has been disposed.
        /// </summary>
		private void AssertNotDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException("DBreezeSession");
        }

        #region IDisposable Members

        /// <summary>true if the instance of the object has been disposed.</summary>
        private bool disposed;

        /// <summary>
        /// Disposes the object via a special disposing task that is executed with the exclusive scheduler.
        /// </summary>
		public void Dispose()
        {
            this.disposed = true;
            if (this.singleThread == null)
                return;

            ManualResetEventSlim cleaned = new ManualResetEventSlim();
            new Task(() =>
            {
                if (this.Transaction != null)
                {
                    this.transaction.Dispose();
                    this.transaction = null;
                }

                if (this.engine != null)
                {
                    this.engine.Dispose();
                    this.engine = null;
                }

                this.singleThread.Dispose();
                this.singleThread = null;

                cleaned.Set();
            }).Start(this.singleThread);

            cleaned.Wait();
        }

        #endregion
    }
}