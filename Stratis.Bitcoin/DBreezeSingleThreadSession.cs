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
	public class DBreezeSingleThreadSession : IDisposable
	{
		public DBreezeSingleThreadSession(string threadName, string folder)
		{
			_SingleThread = new CustomThreadPoolTaskScheduler(1, 100, threadName);
			new Task(() =>
			{
				DBreeze.Utils.CustomSerializator.ByteArraySerializator = NBitcoinSerialize;
				DBreeze.Utils.CustomSerializator.ByteArrayDeSerializator = NBitcoinDeserialize;
				_Engine = new DBreezeEngine(folder);
				_Transaction = _Engine.GetTransaction();
			}).Start(_SingleThread);
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
			throw new NotSupportedException();
		}

		public void Dispose()
		{
			_IsDiposed = true;
			if(_SingleThread == null)
				return;
			ManualResetEventSlim cleaned = new ManualResetEventSlim();
			new Task(() =>
			{
				if(Transaction != null)
				{
					_Transaction.Dispose();
					_Transaction = null;
				}
				if(_Engine != null)
				{
					_Engine.Dispose();
					_Engine = null;
				}
				_SingleThread.Dispose();
				_SingleThread = null;
				cleaned.Set();
			}).Start(_SingleThread);
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
				return _Transaction;
			}
		}

		public Task Do(Action act)
		{
			AssertNotDisposed();
			var task = new Task(() =>
			{
				AssertNotDisposed();
				act();
			});
			task.Start(_SingleThread);
			return task;
		}

		private void AssertNotDisposed()
		{
			if(_IsDiposed)
				throw new ObjectDisposedException("DBreezeSession");
		}

		public Task<T> Do<T>(Func<T> act)
		{
			AssertNotDisposed();
			var task = new Task<T>(() =>
			{
				AssertNotDisposed();
				return act();
			});
			task.Start(_SingleThread);
			return task;
		}
	}
}
