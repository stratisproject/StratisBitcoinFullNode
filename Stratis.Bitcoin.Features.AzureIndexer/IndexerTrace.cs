using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace NBitcoin.Indexer
{
    public class IndexerTrace
    {
		static ILogger _Logger = NullLogger.Instance;
		public static void Configure(ILoggerFactory factory)
		{
			_Logger = factory.CreateLogger("NBitcoin.Indexer");
		}

        internal static void ErrorWhileImportingBlockToAzure(uint256 id, Exception ex)
        {
			_Logger.LogError(ex, "Error while importing " + id + " in azure blob");
        }


        internal static void BlockAlreadyUploaded()
        {
			_Logger.LogDebug("Block already uploaded");
        }

        internal static void BlockUploaded(TimeSpan time, int bytes)
        {
            if (time.TotalSeconds == 0.0)
                time = TimeSpan.FromMilliseconds(10);
            double speed = ((double)bytes / 1024.0) / time.TotalSeconds;
			_Logger.LogDebug("Block uploaded successfully (" + speed.ToString("0.00") + " KB/S)");
        }

        internal static IDisposable NewCorrelation(string activityName)
        {
			return _Logger.BeginScope(activityName);
        }

        internal static void CheckpointLoaded(ChainedBlock block, string checkpointName)
        {
			_Logger.LogInformation("Checkpoint " + checkpointName + " loaded at " + ToString(block));
        }

        internal static void CheckpointSaved(ChainedBlock block, string checkpointName)
        {
			_Logger.LogInformation("Checkpoint " + checkpointName + " saved at " + ToString(block));
        }


        internal static void ErrorWhileImportingEntitiesToAzure(ITableEntity[] entities, Exception ex)
        {
            StringBuilder builder = new StringBuilder();
            int i = 0;
            foreach (var entity in entities)
            {
                builder.AppendLine("[" + i + "] " + entity.RowKey);
                i++;
            }
            _Logger.LogError(ex, "Error while importing entities (len:" + entities.Length + ")\r\n" + builder.ToString());
        }

        internal static void RetryWorked()
        {
            _Logger.LogInformation("Retry worked");
        }

        public static string Pretty(TimeSpan span)
        {
            if (span == TimeSpan.Zero)
                return "0m";

            var sb = new StringBuilder();
            if (span.Days > 0)
                sb.AppendFormat("{0}d ", span.Days);
            if (span.Hours > 0)
                sb.AppendFormat("{0}h ", span.Hours);
            if (span.Minutes > 0)
                sb.AppendFormat("{0}m", span.Minutes);
            var result = sb.ToString();
            if (result == string.Empty)
                return "< 1min";
            return result;
        }

        internal static void TaskCount(int count)
        {
			_Logger.LogInformation("Upload thread count : " + count);
        }

        internal static void ErrorWhileImportingBalancesToAzure(Exception ex, uint256 txid)
        {
			_Logger.LogError(ex, "Error while importing balances on " + txid);
        }

        internal static void MissingTransactionFromDatabase(uint256 txid)
        {
			_Logger.LogError("Missing transaction from index while fetching outputs " + txid);
        }


        internal static void InputChainTip(ChainedBlock block)
        {
            _Logger.LogInformation("The input chain tip is at height " + ToString(block));
        }

        private static string ToString(uint256 blockId, int height)
        {
            return height.ToString();
        }

        internal static void IndexedChainTip(uint256 blockId, int height)
        {
			_Logger.LogInformation("Indexed chain is at height " + ToString(blockId, height));
        }

        internal static void InputChainIsLate()
        {
			_Logger.LogInformation("The input chain is late compared to the indexed one");
        }

        public static void IndexingChain(ChainedBlock from, ChainedBlock to)
        {
			_Logger.LogInformation("Indexing blocks from " + ToString(from) + " to " + ToString(to) + " (both included)");
        }

        private static string ToString(ChainedBlock chainedBlock)
        {
            if (chainedBlock == null)
                return "(null)";
            return ToString(chainedBlock.HashBlock, chainedBlock.Height);
        }

        internal static void RemainingBlockChain(int height, int maxHeight)
        {
            int remaining = height - maxHeight;
            if (remaining % 1000 == 0 && remaining != 0)
            {
				_Logger.LogInformation("Remaining chain block to index : " + remaining + " (" + height + "/" + maxHeight + ")");
            }
        }

        internal static void IndexedChainIsUpToDate(ChainedBlock block)
        {
			_Logger.LogInformation("Indexed chain is up to date at height " + ToString(block));
        }

        public static void Information(string message)
        {
			_Logger.LogInformation(message);
        }

        internal static void NoForkFoundWithStored()
        {
			_Logger.LogInformation("No fork found with the stored chain");
        }

        public static void Processed(int height, int totalHeight, Queue<DateTime> lastLogs, Queue<int> lastHeights)
        {
            var lastLog = lastLogs.LastOrDefault();
            if (DateTime.UtcNow - lastLog > TimeSpan.FromSeconds(10))
            {
                if (lastHeights.Count > 0)
                {
                    var lastHeight = lastHeights.Peek();
                    var time = DateTimeOffset.UtcNow - lastLogs.Peek();

                    var downloadedSize = GetSize(lastHeight, height);
                    var remainingSize = GetSize(height, totalHeight);
                    var estimatedTime = downloadedSize < 1.0m ? TimeSpan.FromDays(999.0)
                        : TimeSpan.FromTicks((long)((remainingSize / downloadedSize) * time.Ticks));
					_Logger.LogInformation("Blocks {0}/{1} (estimate : {2})", height, totalHeight, Pretty(estimatedTime));
                }
                lastLogs.Enqueue(DateTime.UtcNow);
                lastHeights.Enqueue(height);
                while (lastLogs.Count > 20)
                {
                    lastLogs.Dequeue();
                    lastHeights.Dequeue();
                }
            }
        }

        private static decimal GetSize(int t1, int t2)
        {
            decimal cumul = 0.0m;
            for (int i = t1 ; i < t2 ; i++)
            {
				var size = EstimateSize(i);
                cumul += (decimal)size;
            }
            return cumul;
        }

		static int OneMBHeight = 390000;

		private static decimal EstimateSize(decimal height)
        {
			if(height > OneMBHeight)
				return 1.0m;
            return (decimal)Math.Exp((double)(a * height + b));
        }

        static decimal a = 0.0000221438236661323m;
        static decimal b = -8.492328726823666132321613096m;

    }
}
