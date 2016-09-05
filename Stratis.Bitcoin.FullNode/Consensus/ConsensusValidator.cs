using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class ConsensusValidator
	{
		NBitcoin.Consensus _ConsensusParams;
		public ConsensusValidator(NBitcoin.Consensus consensusParams)
		{
			if(consensusParams == null)
				throw new ArgumentNullException("consensusParams");
			_ConsensusParams = consensusParams;
		}

		public bool CheckBlockHeader(BlockHeader header)
		{
			if(!header.CheckProofOfWork())
				return Error("high-hash", "proof of work failed");
			return true;
		}

		public bool ContextualCheckBlockHeader(ContextInformation context, BlockHeader header)
		{
			if(context.BestBlock == null)
				throw new ArgumentException("context.BestBlock should not be null");
			int nHeight = context.BestBlock.Height + 1;

			// Check proof of work
			if(header.Bits != context.NextWorkRequired)
				return Error("bad-diffbits", "incorrect proof of work");

			// Check timestamp against prev
			if(header.BlockTime <= context.BestBlock.MedianTimePast)
				return Error("time-too-old", "block's timestamp is too early");

			// Check timestamp
			if(header.BlockTime > context.Time + TimeSpan.FromHours(2))
				return Error("time-too-new", "block timestamp too far in the future");

			// Reject outdated version blocks when 95% (75% on testnet) of the network has upgraded:
			// check for version 2, 3 and 4 upgrades
			if((header.Version < 2 && nHeight >= _ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34]) ||
			   (header.Version < 3 && nHeight >= _ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]) ||
			   (header.Version < 4 && nHeight >= _ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]))
				return Error("bad-version", $"rejected nVersion={header.Version} block");

			return true;
		}

		private bool Error(string code, string message)
		{
			return false;
		}
	}
}
