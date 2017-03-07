using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin
{
	public interface IBroadcast<in T>
	{
		void Broadcast(T item);
	}

	public abstract class SignalObserver<T> : ObserverBase<T>
	{
		protected override void OnErrorCore(Exception error)
		{
			Logging.Logs.FullNode.LogError(error.ToString());
		}

		protected override void OnCompletedCore()
		{
			// nothing to do
		}
	}

	public class Signaler<T> : IBroadcast<T>, IObservable<T>
	{
		private readonly ISubject<T> subject;
		private readonly IObservable<T> observable;

		public Signaler()
		{
			subject = new Subject<T>();
			subject = Subject.Synchronize(subject);
			observable = subject.AsObservable();
		}

		public IDisposable Subscribe(IObserver<T> observer)
		{
			return observable.Subscribe(observer);
		}

		public void Broadcast(T item)
		{
			subject.OnNext(item);
		}
	}

	public class Signals
	{
		public Signals()
		{
			this.Blocks = new Signaler<Block>();
			this.Transactions = new Signaler<Transaction>();
		}

		public Signaler<Block> Blocks { get; }
		public Signaler<Transaction> Transactions { get; }

		public void Signal(Block block)
		{
			this.Blocks.Broadcast(block);
		}

		public void Signal(Transaction trx)
		{
			this.Transactions.Broadcast(trx);
		}
	}
}
