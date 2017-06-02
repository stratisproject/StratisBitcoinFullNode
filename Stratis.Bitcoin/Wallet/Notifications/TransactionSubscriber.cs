using Stratis.Bitcoin;
using System;
using NBitcoin;

namespace Stratis.Bitcoin.Wallet.Notifications
{
    /// <summary>
    /// Manages the subscription of the transaction observer to the transaction signaler.
    /// </summary>
	public class TransactionSubscriber
	{
		private readonly ISignaler<Transaction> signaler;
		private readonly TransactionObserver observer;

		public TransactionSubscriber(ISignaler<Transaction> signaler, TransactionObserver observer)
		{
			this.signaler = signaler;
			this.observer = observer;			
		}

        /// <summary>
        /// Subscribes the transaction observer to the transaction signaler.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/></returns>
		public IDisposable Subscribe()
		{
			return this.signaler.Subscribe(this.observer);
		}		
	}
}
