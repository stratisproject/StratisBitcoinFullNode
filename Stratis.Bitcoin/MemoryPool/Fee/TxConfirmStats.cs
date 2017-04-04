using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.MemoryPool.Fee
{
	public class TxConfirmStats
	{
		//Define the buckets we will group transactions into
		private List<double> buckets; // The upper-bound of the range for the bucket (inclusive)
		private Dictionary<double, int> bucketMap; // Map of bucket upper-bound to index into all vectors by bucket

		// For each bucket X:
		// Count the total # of txs in each bucket
		// Track the historical moving average of this total over blocks
		private List<double> txCtAvg;
		// and calculate the total for the current block to update the moving average
		private List<int> curBlockTxCt;

		// Count the total # of txs confirmed within Y blocks in each bucket
		// Track the historical moving average of theses totals over blocks
		private List<List<double>> confAvg; // confAvg[Y][X]
		// and calculate the totals for the current block to update the moving averages
		private List<List<int>> curBlockConf; // curBlockConf[Y][X]

		// Sum the total feerate of all tx's in each bucket
		// Track the historical moving average of this total over blocks
		private List<double> avg;
		// and calculate the total for the current block to update the moving average
		private List<double> curBlockVal;

		// Combine the conf counts with tx counts to calculate the confirmation % for each Y,X
		// Combine the total value with the tx counts to calculate the avg feerate per bucket

		private double decay;

		// Mempool counts of outstanding transactions
		// For each bucket X, track the number of transactions in the mempool
		// that are unconfirmed for each possible confirmation value Y
		private List<List<int>> unconfTxs; //unconfTxs[Y][X]
		// transactions still unconfirmed after MAX_CONFIRMS for each bucket
		private List<int> oldUnconfTxs;

		// Initialize the data structures.  This is called by BlockPolicyEstimator's
		// constructor with default values.
		// @param defaultBuckets contains the upper limits for the bucket boundaries
		// @param maxConfirms max number of confirms to track
		// @param decay how much to decay the historical moving average per block

		public void Initialize(List<double> defaultBuckets, int maxConfirms, double decay)
		{
			buckets = new List<double>();
			bucketMap = new Dictionary<double, int>();

			this.decay = decay;
			for (int i = 0; i < defaultBuckets.Count; i++)
			{
				buckets.Add(defaultBuckets[i]);
				bucketMap[defaultBuckets[i]] = i;
			}
			confAvg = new List<List<double>>();
			curBlockConf = new List<List<int>>();
			unconfTxs = new List<List<int>>();

			for (int i = 0; i < maxConfirms; i++)
			{
				confAvg.Insert(i, Enumerable.Repeat(default(double), buckets.Count).ToList());
				curBlockConf.Insert(i, Enumerable.Repeat(default(int), buckets.Count).ToList());
				unconfTxs.Insert(i, Enumerable.Repeat(default(int), buckets.Count).ToList());
			}

			oldUnconfTxs = new List<int>(Enumerable.Repeat(default(int), buckets.Count));
			curBlockTxCt = new List<int>(Enumerable.Repeat(default(int), buckets.Count));
			txCtAvg = new List<double>(Enumerable.Repeat(default(double), buckets.Count));
			curBlockVal = new List<double>(Enumerable.Repeat(default(double), buckets.Count));
			avg = new List<double>(Enumerable.Repeat(default(double), buckets.Count));
		}

		// Clear the state of the curBlock variables to start counting for the new block 
		public void ClearCurrent(int nBlockHeight)
		{
			for (int j = 0; j < buckets.Count; j++)
			{
				oldUnconfTxs[j] += unconfTxs[nBlockHeight%unconfTxs.Count][j];
				unconfTxs[nBlockHeight%unconfTxs.Count][j] = 0;
				for (int i = 0; i < curBlockConf.Count; i++)
					curBlockConf[i][j] = 0;
				curBlockTxCt[j] = 0;
				curBlockVal[j] = 0;
			}
		}


		// Record a new transaction data point in the current block stats
		// @param blocksToConfirm the number of blocks it took this transaction to confirm
		// @param val the feerate of the transaction
		// @warning blocksToConfirm is 1-based and has to be >= 1
		public void Record(int blocksToConfirm, double val)
		{
			// blocksToConfirm is 1-based
			if (blocksToConfirm < 1)
				return;
			int bucketindex = bucketMap.FirstOrDefault(k => k.Key > val).Value;
			for (var i = blocksToConfirm; i <= curBlockConf.Count; i++)
			{
				curBlockConf[i - 1][bucketindex]++;
			}
			curBlockTxCt[bucketindex]++;
			curBlockVal[bucketindex] += val;
		}

		// Record a new transaction entering the mempool
		public int NewTx(int nBlockHeight, double val)
		{
			int bucketindex = bucketMap.FirstOrDefault(k => k.Key > val).Value;
			int blockIndex = nBlockHeight%unconfTxs.Count;
			unconfTxs[blockIndex][bucketindex]++;
			return bucketindex;
		}

		// Remove a transaction from mempool tracking stats
		public void RemoveTx(int entryHeight, int nBestSeenHeight, int bucketIndex)
		{
			//nBestSeenHeight is not updated yet for the new block
			int blocksAgo = nBestSeenHeight - entryHeight;
			if (nBestSeenHeight == 0) // the BlockPolicyEstimator hasn't seen any blocks yet
				blocksAgo = 0;
			if (blocksAgo < 0)
			{
				Logging.Logs.EstimateFee.LogInformation($"Blockpolicy error, blocks ago is negative for mempool tx");
				return; //This can't happen because we call this with our best seen height, no entries can have higher
			}

			if (blocksAgo >= (int) unconfTxs.Count)
			{
				if (oldUnconfTxs[bucketIndex] > 0)
				{
					oldUnconfTxs[bucketIndex]--;
				}
				else
				{
					Logging.Logs.EstimateFee.LogInformation(
						$"Blockpolicy error, mempool tx removed from >25 blocks,bucketIndex={bucketIndex} already");
				}
			}
			else
			{
				int blockIndex = entryHeight%unconfTxs.Count;
				if (unconfTxs[blockIndex][bucketIndex] > 0)
				{
					unconfTxs[blockIndex][bucketIndex]--;
				}
				else
				{
					Logging.Logs.EstimateFee.LogInformation(
						$"Blockpolicy error, mempool tx removed from blockIndex={blockIndex},bucketIndex={bucketIndex} already");
				}
			}
		}

		// Update our estimates by decaying our historical moving average and updating
		//	with the data gathered from the current block 

		public void UpdateMovingAverages()
		{
			for (int j = 0; j < buckets.Count; j++)
			{
				for (int i = 0; i < confAvg.Count; i++)
					confAvg[i][j] = confAvg[i][j]*decay + curBlockConf[i][j];
				avg[j] = avg[j]*decay + curBlockVal[j];
				txCtAvg[j] = txCtAvg[j]*decay + curBlockTxCt[j];
			}
		}

		// Calculate a feerate estimate.  Find the lowest value bucket (or range of buckets
		// to make sure we have enough data points) whose transactions still have sufficient likelihood
		// of being confirmed within the target number of confirmations
		// @param confTarget target number of confirmations
		// @param sufficientTxVal required average number of transactions per block in a bucket range
		// @param minSuccess the success probability we require
		// @param requireGreater return the lowest feerate such that all higher values pass minSuccess OR
		//        return the highest feerate such that all lower values fail minSuccess
		// @param nBlockHeight the current block height
		public double EstimateMedianVal(int confTarget, double sufficientTxVal, double successBreakPoint, bool requireGreater,
			int nBlockHeight)
		{
			// Counters for a bucket (or range of buckets)
			double nConf = 0; // Number of tx's confirmed within the confTarget
			double totalNum = 0; // Total number of tx's that were ever confirmed
			int extraNum = 0; // Number of tx's still in mempool for confTarget or longer

			int maxbucketindex = buckets.Count - 1;

			// requireGreater means we are looking for the lowest feerate such that all higher
			// values pass, so we start at maxbucketindex (highest feerate) and look at successively
			// smaller buckets until we reach failure.  Otherwise, we are looking for the highest
			// feerate such that all lower values fail, and we go in the opposite direction.
			int startbucket = requireGreater ? maxbucketindex : 0;
			int step = requireGreater ? -1 : 1;

			// We'll combine buckets until we have enough samples.
			// The near and far variables will define the range we've combined
			// The best variables are the last range we saw which still had a high
			// enough confirmation rate to count as success.
			// The cur variables are the current range we're counting.
			int curNearBucket = startbucket;
			int bestNearBucket = startbucket;
			int curFarBucket = startbucket;
			int bestFarBucket = startbucket;

			bool foundAnswer = false;
			int bins = unconfTxs.Count;

			// Start counting from highest(default) or lowest feerate transactions
			for (int bucket = startbucket; bucket >= 0 && bucket <= maxbucketindex; bucket += step)
			{
				curFarBucket = bucket;
				nConf += confAvg[confTarget - 1][bucket];
				totalNum += txCtAvg[bucket];
				for (int confct = confTarget; confct < GetMaxConfirms(); confct++)
					extraNum += unconfTxs[(nBlockHeight - confct)%bins][bucket];
				extraNum += oldUnconfTxs[bucket];
				// If we have enough transaction data points in this range of buckets,
				// we can test for success
				// (Only count the confirmed data points, so that each confirmation count
				// will be looking at the same amount of data and same bucket breaks)
				if (totalNum >= sufficientTxVal/(1 - decay))
				{
					double curPct = nConf/(totalNum + extraNum);

					// Check to see if we are no longer getting confirmed at the success rate
					if (requireGreater && curPct < successBreakPoint)
						break;
					if (!requireGreater && curPct > successBreakPoint)
						break;

					// Otherwise update the cumulative stats, and the bucket variables
					// and reset the counters
					else
					{
						foundAnswer = true;
						nConf = 0;
						totalNum = 0;
						extraNum = 0;
						bestNearBucket = curNearBucket;
						bestFarBucket = curFarBucket;
						curNearBucket = bucket + step;
					}
				}
			}

			double median = -1;
			double txSum = 0;

			// Calculate the "average" feerate of the best bucket range that met success conditions
			// Find the bucket with the median transaction and then report the average feerate from that bucket
			// This is a compromise between finding the median which we can't since we don't save all tx's
			// and reporting the average which is less accurate
			int minBucket = bestNearBucket < bestFarBucket ? bestNearBucket : bestFarBucket;
			int maxBucket = bestNearBucket > bestFarBucket ? bestNearBucket : bestFarBucket;
			for (int j = minBucket; j <= maxBucket; j++)
			{
				txSum += txCtAvg[j];
			}
			if (foundAnswer && txSum != 0)
			{
				txSum = txSum/2;
				for (int j = minBucket; j <= maxBucket; j++)
				{
					if (txCtAvg[j] < txSum)
						txSum -= txCtAvg[j];
					else
					{
						// we're in the right bucket
						median = avg[j]/txCtAvg[j];
						break;
					}
				}
			}

			Logging.Logs.EstimateFee.LogInformation(
				$"{confTarget}: For conf success {(requireGreater ? $">" : $"<")} {successBreakPoint} need feerate {(requireGreater ? $">" : $"<")}: {median} from buckets {buckets[minBucket]} -{buckets[maxBucket]}  Cur Bucket stats {100*nConf/(totalNum + extraNum)}  {nConf}/({totalNum}+{extraNum} mempool)");

			return median;
		}

		// Return the max number of confirms we're tracking 
		public int GetMaxConfirms()
		{
			return confAvg.Count;
		}

		// Write state of estimation data to a file
		public void Write(BitcoinStream stream)
		{
		}

		// Read saved state of estimation data from a file and replace all internal data structures and
		// variables with this state.
		public void Read(Stream filein)
		{
		}
	}
}