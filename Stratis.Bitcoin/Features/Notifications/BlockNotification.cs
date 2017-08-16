using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Notifications
{
	/// <summary>
	/// Class used to broadcast about new blocks.
	/// </summary>
	public class BlockNotification
	{
		private readonly ISignals signals;
        private readonly IAsyncLoopFactory asyncLoopFactory;
	    private readonly INodeLifetime nodeLifetime;

	    private ChainedBlock tip;

        public BlockNotification(ConcurrentChain chain, ILookaheadBlockPuller puller, ISignals signals, 
            IAsyncLoopFactory asyncLoopFactory, INodeLifetime nodeLifetime)
		{
			Guard.NotNull(chain, nameof(chain));
			Guard.NotNull(puller, nameof(puller));
			Guard.NotNull(signals, nameof(signals));
            Guard.NotNull(asyncLoopFactory, nameof(asyncLoopFactory));

			this.Chain = chain;
			this.Puller = puller;
			this.signals = signals;
            this.asyncLoopFactory = asyncLoopFactory;
		    this.nodeLifetime = nodeLifetime;
		}

		public ILookaheadBlockPuller Puller { get; }

		public ConcurrentChain Chain { get; }

		public uint256 StartHash { get; private set; }

		private bool reSync;

		public void SyncFrom(uint256 startHash)
		{
			if (this.StartHash != null)
			{
				this.reSync = true;
			    ChainedBlock startBlock = this.Chain.GetBlock(startHash);
			    if (startBlock != null)
			    {
			        // sets the location of the puller to the latest hash that was broadcasted
			        this.Puller.SetLocation(startBlock);
                    this.tip = startBlock;
                }
                
            }

			this.StartHash = startHash;
		}
		
		/// <summary>
		/// Notifies about blocks, starting from block with hash passed as parameter.
		/// </summary>
		public virtual Task Notify()
		{
			return this.asyncLoopFactory.Run("block notifier", token =>
			{
				// if the StartHash hasn't been set yet
				if (this.StartHash == null)
				{
					return Task.CompletedTask;
				}

				// make sure the chain has been downloaded
				ChainedBlock startBlock = this.Chain.GetBlock(this.StartHash);
				if (startBlock == null)
				{
					return Task.CompletedTask;
				}

				// sets the location of the puller to the latest hash that was broadcasted
				this.Puller.SetLocation(startBlock);
                this.tip = startBlock;

				// send notifications for all the following blocks
				while (!this.reSync)
				{					
					var block = this.Puller.NextBlock(token);

					if (block != null)
					{
						// broadcast the block to the registered observers
						this.signals.SignalBlock(block);
                        this.tip = this.Chain.GetBlock(block.GetHash());
					}
					else
					{
                        // in reorg we reset the puller to the fork
                        // when a reorg happens the puller is pushed
                        // back and continues from the current fork

                        // find the fork
                        while (this.Chain.GetBlock(this.tip.HashBlock) == null)
                            this.tip = this.tip.Previous;

                        // set the puller to the fork location
                        this.Puller.SetLocation(this.tip);
					}
				}
				
				this.reSync = false;

				return Task.CompletedTask;
			}, this.nodeLifetime.ApplicationStopping);
		}		
	}
}
