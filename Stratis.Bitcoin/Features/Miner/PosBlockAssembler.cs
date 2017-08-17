using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{

	public class PosBlockAssembler : PowBlockAssembler
	{
		private readonly StakeChain stakeChain;

		public PosBlockAssembler(
            ConsensusLoop consensusLoop, 
            Network network, 
            ConcurrentChain chain,
			MempoolAsyncLock mempoolScheduler, 
            TxMempool mempool,
			IDateTimeProvider dateTimeProvider, 
            StakeChain stakeChain,
            ILogger logger,
            AssemblerOptions options = null)
			: base(consensusLoop, network, chain, mempoolScheduler, mempool, dateTimeProvider, logger, options)
		{
			this.stakeChain = stakeChain;
		}


		public override BlockTemplate CreateNewBlock(Script scriptPubKeyIn, bool fMineWitnessTx = true)
		{
			base.CreateNewBlock(scriptPubKeyIn, fMineWitnessTx);

			this.coinbase.Outputs[0].ScriptPubKey = new Script();
			this.coinbase.Outputs[0].Value = Money.Zero;

			var posvalidator = this.consensusLoop.Validator as PosConsensusValidator;
			Guard.NotNull(posvalidator, "posvalidator");

			// TODO: add this code
			// Timestamp limit
			// if (tx.nTime > GetAdjustedTime() || (fProofOfStake && tx.nTime > pblock->vtx[0].nTime))
				//continue;

			return this.pblocktemplate;
		}

		protected override void UpdateHeaders()
		{
			base.UpdateHeaders();

			var stake = new BlockStake(this.pblock);
			this.pblock.Header.Bits = StakeValidator.GetNextTargetRequired(this.stakeChain, this.chain.Tip, this.network.Consensus, this.options.IsProofOfStake);
		}

		protected override void TestBlockValidity()
		{
			//base.TestBlockValidity();
		}
	}
}
