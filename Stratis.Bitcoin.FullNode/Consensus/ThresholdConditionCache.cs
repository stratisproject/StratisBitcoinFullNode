using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class ThresholdConditionCache
	{
		/** What block version to use for new blocks (pre versionbits) */
		static int VERSIONBITS_LAST_OLD_BLOCK_VERSION = 4;
		/** What bits to set in version for versionbits blocks */
		static uint VERSIONBITS_TOP_BITS = 0x20000000;
		/** What bitmask determines whether versionbits is in use */
		static uint VERSIONBITS_TOP_MASK = 0xE0000000;
		/** Total bits available for versionbits */
		static int VERSIONBITS_NUM_BITS = 29;

		readonly static int ArraySize;
		static ThresholdConditionCache()
		{
			ArraySize = Enum.GetValues(typeof(BIP9Deployments)).Length;
		}

		NBitcoin.Consensus _Consensus;
		BIP9Deployments _Deployment;
		Dictionary<uint256, ThresholdState> cache = new Dictionary<uint256, ThresholdState>();

		public ThresholdConditionCache(NBitcoin.Consensus consensus, BIP9Deployments deployment)
		{
			if(consensus == null)
				throw new ArgumentNullException("consensus");
			_Consensus = consensus;
		}
		//public ThresholdState this[uint256 hash]
		//{
		//	get
		//	{

		//	}
		//	set
		//	{ /* set the specified index to value here */
		//	}
		//} 

		public ThresholdState GetState(ChainedBlock pindexPrev)
		{
			int nPeriod = _Consensus.MinerConfirmationWindow;
			int nThreshold = _Consensus.RuleChangeActivationThreshold;
			var nTimeStart = _Consensus.BIP9Deployments[_Deployment].StartTime;
			var nTimeTimeout = _Consensus.BIP9Deployments[_Deployment].Timeout;

			// A block's state is always the same as that of the first of its period, so it is computed based on a pindexPrev whose height equals a multiple of nPeriod - 1.
			if(pindexPrev != null)
			{
				pindexPrev = pindexPrev.GetAncestor(pindexPrev.Height - ((pindexPrev.Height + 1) % nPeriod));
			}

			// Walk backwards in steps of nPeriod to find a pindexPrev whose information is known
			List<ChainedBlock> vToCompute = new List<ChainedBlock>();
			while(!cache.ContainsKey(pindexPrev.HashBlock))
			{
				if(pindexPrev == null)
				{
					// The genesis block is by definition defined.
					cache[uint256.Zero] = ThresholdState.Defined;
					break;
				}
				if(pindexPrev.GetMedianTimePast() < nTimeStart)
				{
					// Optimization: don't recompute down further, as we know every earlier block will be before the start time
					cache[pindexPrev.HashBlock] = ThresholdState.Defined;
					break;
				}
				vToCompute.Add(pindexPrev);
				pindexPrev = pindexPrev.GetAncestor(pindexPrev.Height - nPeriod);
			}
			// At this point, cache[pindexPrev] is known
			assert(cache.ContainsKey(pindexPrev.HashBlock));
			ThresholdState state = cache[pindexPrev.HashBlock];

			// Now walk forward and compute the state of descendants of pindexPrev
			while(vToCompute.Count != 0)
			{
				ThresholdState stateNext = state;
				pindexPrev = vToCompute[vToCompute.Count - 1];
				vToCompute.RemoveAt(vToCompute.Count - 1);

				switch(state)
				{
					case ThresholdState.Defined:
						{
							if(pindexPrev.GetMedianTimePast() >= nTimeTimeout)
							{
								stateNext = ThresholdState.Failed;
							}
							else if(pindexPrev.GetMedianTimePast() >= nTimeStart)
							{
								stateNext = ThresholdState.Started;
							}
							break;
						}
					case ThresholdState.Started:
						{
							if(pindexPrev.GetMedianTimePast() >= nTimeTimeout)
							{
								stateNext = ThresholdState.Failed;
								break;
							}
							// We need to count
							ChainedBlock pindexCount = pindexPrev;
							int count = 0;
							for(int i = 0; i < nPeriod; i++)
							{
								if(Condition(pindexCount))
								{
									count++;
								}
								pindexCount = pindexCount.Previous;
							}
							if(count >= nThreshold)
							{
								stateNext = ThresholdState.LockedIn;
							}
							break;
						}
					case ThresholdState.LockedIn:
						{
							// Always progresses into ACTIVE.
							stateNext = ThresholdState.Active;
							break;
						}
					case ThresholdState.Failed:
					case ThresholdState.Active:
						{
							// Nothing happens, these are terminal states.
							break;
						}
				}
				cache[pindexPrev.HashBlock] = state = stateNext;
			}

			return state;
		}

		private bool Condition(ChainedBlock pindex)
		{
			return (((pindex.Header.Version & VERSIONBITS_TOP_MASK) == VERSIONBITS_TOP_BITS) && (pindex.Header.Version & Mask()) != 0);
		}

		private uint Mask()
		{
			return ((uint)1) << _Consensus.BIP9Deployments[_Deployment].Bit;
		}

		private void assert(bool v)
		{
			if(!v)
				throw new Exception("Assertion failed");
		}
	}
}
