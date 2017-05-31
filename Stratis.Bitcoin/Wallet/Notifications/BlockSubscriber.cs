using Stratis.Bitcoin;
using System;
using NBitcoin;

namespace Stratis.Bitcoin.Wallet.Notifications
{
    /// <summary>
    /// Manages the subscription of the block observer to the block signaler.
    /// </summary>
	public class BlockSubscriber
	{
		private readonly ISignaler<Block> signaler;
		private readonly BlockObserver observer;

		public BlockSubscriber(ISignaler<Block> signaler, BlockObserver observer)
		{
			this.signaler = signaler;
			this.observer = observer;			
		}

        /// <summary>
        /// Subscribes the block observer to the block signaler.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/></returns>
		public IDisposable Subscribe()
		{
			return this.signaler.Subscribe(this.observer);
		}		
	}
}
