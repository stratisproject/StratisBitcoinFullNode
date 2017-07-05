using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.MemoryPool
{
	public class MempoolScheduler : AsyncLock
	{ }

	public class MempoolManager
	{
		private IMempoolPersistence mempoolPersistence;
	    private readonly ILogger mempoolLogger;

        public MempoolScheduler MempoolScheduler { get; }
		public MempoolValidator Validator { get; } // public for testing
		public MempoolOrphans Orphans { get; } // public for testing
		private readonly TxMempool memPool;

		public IDateTimeProvider DateTimeProvider { get; }
		public NodeSettings NodeArgs { get; set; }


		public MempoolManager(
            MempoolScheduler mempoolScheduler, 
            TxMempool memPool,
			MempoolValidator validator, 
            MempoolOrphans orphans, 
            IDateTimeProvider dateTimeProvider, 
            NodeSettings nodeArgs, 
            IMempoolPersistence mempoolPersistence,
            ILoggerFactory loggerFactory)
		{
			this.MempoolScheduler = mempoolScheduler;
			this.memPool = memPool;
			this.DateTimeProvider = dateTimeProvider;
			this.NodeArgs = nodeArgs;
			this.Orphans = orphans;
			this.Validator = validator;
			this.mempoolPersistence = mempoolPersistence;
		    this.mempoolLogger = loggerFactory.CreateLogger("Stratis.Bitcoin.MemoryPool");
        }

        public MempoolPerformanceCounter PerformanceCounter => this.Validator.PerformanceCounter;

		public Task<List<uint256>> GetMempoolAsync()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.MapTx.Keys.ToList());
		}

		public List<TxMempoolInfo> InfoAll()
		{
			// TODO: DepthAndScoreComparator

			return this.memPool.MapTx.DescendantScore.Select(item => new TxMempoolInfo
			{
				Trx = item.Transaction,
				Time = item.Time,
				FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
				FeeDelta = item.ModifiedFee - item.Fee
			}).ToList();
		}

		internal async Task LoadPool(string fileName = null)
		{
			if (this.mempoolPersistence != null && this.memPool?.MapTx != null && this.Validator != null)
			{
			    this.mempoolLogger.LogInformation("Loading Memory Pool...");
				IEnumerable<MempoolPersistenceEntry> entries = this.mempoolPersistence.Load(fileName);
				int i = 0;
				if (entries != null)
				{
				    this.mempoolLogger.LogInformation($"...loaded {entries.Count()} cached entries.");
					foreach (MempoolPersistenceEntry entry in entries)
					{
						Transaction trx = entry.Tx;
						uint256 trxHash = trx.GetHash();
						if (!this.memPool.Exists(trxHash))
						{
							MempoolValidationState state = new MempoolValidationState(false) { AcceptTime = entry.Time, OverrideMempoolLimit = true };
							if (await this.Validator.AcceptToMemoryPoolWithTime(state, trx) && this.memPool.MapTx.ContainsKey(trxHash))
							{
								i++;
								this.memPool.MapTx[trxHash].UpdateFeeDelta(entry.FeeDelta);
							}
						}
					}
				    this.mempoolLogger.LogInformation($"...{i} entries accepted.");
				}
				else
				{
				    this.mempoolLogger.LogInformation($"...Unable to load memory pool cache from {fileName}.");
				}

			}
		}

		internal MemPoolSaveResult SavePool()
		{
			if (this.mempoolPersistence == null)
				return MemPoolSaveResult.NonSuccess;
			return this.mempoolPersistence.Save(this.memPool);
		}

		public TxMempoolInfo Info(uint256 hash)
		{
			TxMempoolEntry item = this.memPool.MapTx.TryGet(hash);
			return item == null ? null : new TxMempoolInfo
			{
				Trx = item.Transaction,
				Time = item.Time,
				FeeRate = new FeeRate(item.Fee, (int)item.GetTxSize()),
				FeeDelta = item.ModifiedFee - item.Fee
			};
		}

		public Task<List<TxMempoolInfo>> InfoAllAsync()
		{
			return this.MempoolScheduler.ReadAsync(this.InfoAll);

		}
		public Task<TxMempoolInfo> InfoAsync(uint256 hash)
		{
			return this.MempoolScheduler.ReadAsync(() => this.Info(hash));
		}

		public Task<long> MempoolSize()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.Size);
		}

		public Task Clear()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.Clear());
		}

		public Task<long> MempoolDynamicMemoryUsage()
		{
			return this.MempoolScheduler.ReadAsync(() => this.memPool.DynamicMemoryUsage());
		}

		public Task RemoveForBlock(Block block, int blockHeight)
		{
			//if (this.IsInitialBlockDownload)
			//	return Task.CompletedTask;

			return this.MempoolScheduler.WriteAsync(() =>
			{
				this.memPool.RemoveForBlock(block.Transactions, blockHeight);

				this.Validator.PerformanceCounter.SetMempoolSize(this.memPool.Size);
				this.Validator.PerformanceCounter.SetMempoolDynamicSize(this.memPool.DynamicMemoryUsage());
			});
		}
	}
}
