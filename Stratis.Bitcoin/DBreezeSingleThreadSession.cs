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

namespace Stratis.Bitcoin
{
    public class DBreezeSingleThreadSession : IDisposable
    {
		public DBreezeSingleThreadSession(string threadName, string folder)
		{
			_SingleThread = new CustomThreadPoolTaskScheduler(1, 100, threadName );
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
			if(type == typeof(uint256))
			{
				return new uint256(bytes);
			}
			throw new NotSupportedException();
		}

		public void Dispose()
		{
			if(_SingleThread == null)
				return;
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
			}).Start(_SingleThread);
			_SingleThread.WaitFinished();
			if(_SingleThread != null)
			{
				_SingleThread.Dispose();
				_SingleThread = null;
			}
		}

		

		private DBreeze.Transactions.Transaction _Transaction;
		private DBreezeEngine _Engine;
		private CustomThreadPoolTaskScheduler _SingleThread;

		public DBreeze.Transactions.Transaction Transaction
		{
			get
			{
				return _Transaction;
			}
		}

		public Task Do(Action act)
		{
			var task = new Task(() =>
			{
				act();
			});
			task.Start(_SingleThread);
			return task;
		}

		public Task<T> Do<T>(Func<T> act)
		{
			var task = new Task<T>(() =>
			{
				return act();
			});
			task.Start(_SingleThread);
			return task;
		}
	}
}
