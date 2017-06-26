using DBreeze;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DBreeze.Transactions;
using System.Threading;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin
{
    public interface IDBreezeSingleThreadSession : IDisposable
    {
        DBreeze.Transactions.Transaction Transaction { get; }

        Task Do(Action act);
        Task<T> Do<T>(Func<T> act);
    }

    public class DBreezeSingleThreadSession : IDBreezeSingleThreadSession
    {
		public DBreezeSingleThreadSession(string threadName, string folder)
		{
            Guard.NotEmpty(threadName, nameof(threadName));
            Guard.NotEmpty(folder, nameof(folder));

            this._SingleThread = new CustomThreadPoolTaskScheduler(1, 100, threadName);
			new Task(() =>
			{
				DBreeze.Utils.CustomSerializator.ByteArraySerializator = NBitcoinSerialize;
				DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = NBitcoinDeserialize;
                this._Engine = new DBreezeEngine(folder);
				this._Transaction = this._Engine.GetTransaction();
			}).Start(this._SingleThread);
		}

		internal static byte[] NBitcoinSerialize(object obj)
		{
			IBitcoinSerializable serializable = obj as IBitcoinSerializable;
			if(serializable != null)
				return serializable.ToBytes();
			uint256 u = obj as uint256;
			if(u != null)
				return u.ToBytes();
			throw new NotSupportedException();
		}

		internal static object NBitcoinDeserialize(byte[] bytes, Type type)
		{
			if(type == typeof(Coins))
			{
				Coins coin = new Coins();
				coin.ReadWrite(bytes);
				return coin;
			}
			if(type == typeof(BlockHeader))
			{
				BlockHeader header = new BlockHeader();
				header.ReadWrite(bytes);
				return header;
			}
			if(type == typeof(RewindData))
			{
				RewindData rewind = new RewindData();
				rewind.ReadWrite(bytes);
				return rewind;
			}
			if(type == typeof(uint256))
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

		public void Dispose()
		{
            this._IsDiposed = true;
			if(this._SingleThread == null)
				return;
			ManualResetEventSlim cleaned = new ManualResetEventSlim();
			new Task(() =>
			{
				if(this.Transaction != null)
				{
                    this._Transaction.Dispose();
					this._Transaction = null;
				}
				if(this._Engine != null)
				{
                    this._Engine.Dispose();
					this._Engine = null;
				}
                this._SingleThread.Dispose();
				this._SingleThread = null;
				cleaned.Set();
			}).Start(this._SingleThread);
			cleaned.Wait();
		}

		private DBreeze.Transactions.Transaction _Transaction;
		private DBreezeEngine _Engine;
		private CustomThreadPoolTaskScheduler _SingleThread;
		bool _IsDiposed;
		public DBreeze.Transactions.Transaction Transaction
		{
			get
			{
				return this._Transaction;
			}
		}

		public Task Do(Action act)
		{
            Guard.NotNull(act, nameof(act));

			AssertNotDisposed();
			var task = new Task(() =>
			{
				AssertNotDisposed();
				act();
			});
			task.Start(this._SingleThread);
			return task;
		}

		private void AssertNotDisposed()
		{
			if(this._IsDiposed)
				throw new ObjectDisposedException("DBreezeSession");
		}

		public Task<T> Do<T>(Func<T> act)
		{
            Guard.NotNull(act, nameof(act));

            AssertNotDisposed();
			var task = new Task<T>(() =>
			{
				AssertNotDisposed();
				return act();
			});
			task.Start(this._SingleThread);
			return task;
		}
	}
}