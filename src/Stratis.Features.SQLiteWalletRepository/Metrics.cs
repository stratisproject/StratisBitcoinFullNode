using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Stratis.Features.SQLiteWalletRepository.Commands;
using Stratis.Features.SQLiteWalletRepository.Tables;

namespace Stratis.Features.SQLiteWalletRepository
{
    /// <summary>
    /// Tracks metrics.
    /// </summary>
    internal class Metrics
    {
        public long ReadTime;
        public int ReadCount;
        public long ProcessTime;
        public int ProcessCount;
        public long BlockTime;
        public int BlockCount;
        public long CommitTime;
        public string Path;

        public Metrics(string path)
        {
            this.Path = path;
        }

        public void LogMetrics(SQLiteWalletRepository repo, DBConnection conn, ChainedHeader header, HDWallet wallet)
        {
            // Write some metrics to file.
            if (repo.WriteMetricsToFile)
            {
                string fixedWidth(object val, int width)
                {
                    return string.Format($"{{0,{width}}}", val);
                }

                var lines = new List<string>();

                lines.Add($"--- Date/Time: {(DateTime.Now.ToString())}, Block Height: { (header?.Height) } ---");

                var processCnt = fixedWidth(this.ProcessCount, 5);
                var processTime = fixedWidth(((double)this.ProcessTime / 10_000_000).ToString("N06"), 8);
                var processAvgSec = fixedWidth(((double)this.ProcessTime / this.ProcessCount / 10_000_000).ToString("N06"), 8);

                var scanCnt = fixedWidth(this.BlockCount, 5);
                var scanTime = fixedWidth(((double)this.BlockTime / 10_000_000).ToString("N06"), 8);
                var scanAvgSec = fixedWidth(((double)this.BlockTime / this.BlockCount / 10_000_000).ToString("N06"), 8);

                var readCnt = fixedWidth(this.ReadCount, 5);
                var readTime = fixedWidth(((double)this.ReadTime / 10_000_000).ToString("N06"), 8);
                var readAvgSec = fixedWidth(((double)this.ReadTime / this.ReadCount / 10_000_000).ToString("N06"), 8);

                var commitTime = fixedWidth(((double)this.CommitTime / 10_000_000).ToString("N06"), 8);
                var commitAvgSec = fixedWidth(((double)this.CommitTime / this.ProcessCount / 10_000_000).ToString("N06"), 8);

                lines.Add($"{fixedWidth("Blocks Scanned", -20)  }: Time={scanTime   }, Count={scanCnt   }, AvgSec={scanAvgSec}");
                lines.Add($"{fixedWidth("-Blocks Read", -20)     }: Time={readTime   }, Count={readCnt   }, AvgSec={readAvgSec}");
                lines.Add($"{fixedWidth("Blocks Processed", -20)}: Time={processTime}, Count={processCnt}, AvgSec={processAvgSec}");

                foreach ((string cmdName, DBCommand cmd) in conn.Commands.Select(kv => (kv.Key, kv.Value)))
                {
                    var key = fixedWidth($"-{cmdName}", -20);
                    var time = fixedWidth(((double)cmd.ProcessTime / 10_000_000).ToString("N06"), 8);
                    var count = fixedWidth(cmd.ProcessCount, 5);
                    var avgsec = (cmd.ProcessCount == 0) ? null : fixedWidth(((double)cmd.ProcessTime / cmd.ProcessCount / 10_000_000).ToString("N06"), 8);

                    lines.Add($"{key}: Time={time}, Count={count}, AvgSec={avgsec}");
                }

                lines.Add($"{fixedWidth("-Commit", -20)}: Time={commitTime}, Count={processCnt}, AvgSec={commitAvgSec}");

                lines.Add("");

                this.BlockCount = 0;
                this.BlockTime = 0;
                this.ProcessCount = 0;
                this.ProcessTime = 0;
                this.ReadCount = 0;
                this.ReadTime = 0;
                this.CommitTime = 0;

                foreach (var kv in conn.Commands)
                {
                    kv.Value.ProcessCount = 0;
                    kv.Value.ProcessTime = 0;
                }

                if (wallet != null)
                    File.AppendAllLines(System.IO.Path.Combine(this.Path, $"Metrics_{ wallet.Name }.txt"), lines);
                else
                    File.AppendAllLines(System.IO.Path.Combine(this.Path, "Metrics.txt"), lines);
            }
        }
    }
}
