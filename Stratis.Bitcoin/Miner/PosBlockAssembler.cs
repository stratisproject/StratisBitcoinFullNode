using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Miner
{

	public class PosBlockAssembler : PowBlockAssembler
	{
		private readonly StakeChain stakeChain;

		public PosBlockAssembler(ConsensusLoop consensusLoop, Network network, ConcurrentChain chain,
			MempoolScheduler mempoolScheduler, TxMempool mempool,
			IDateTimeProvider dateTimeProvider, StakeChain stakeChain, AssemblerOptions options = null)
			: base(consensusLoop, network, chain, mempoolScheduler, mempool, dateTimeProvider, options)
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
			this.pblock.Header.Bits = StakeValidator.GetNextTargetRequired(stakeChain, this.chain.Tip, this.network.Consensus, this.options.IsProofOfStake);
		}

		protected override void TestBlockValidity()
		{
			//base.TestBlockValidity();
		}
	}
}
