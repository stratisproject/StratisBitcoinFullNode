namespace Stratis.Benchmark.Persistence.Faster
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BenchmarkDotNet.Attributes;
    using Bitcoin.Networks;
    using FASTER.core;
    using NBitcoin;
    using Stratis.Benchmark.Infrastructure.Faster;
    using Stratis.Bitcoin.Features.Consensus;

    [RankColumn]
    public class MsFasterFetchCoin : NetworkBenchmarkBase
    {
        [Params(1000)]
        public int N;

        private uint256[] data;
        private IDevice log;
        private FasterKV<CacheKey, CacheValue, CacheInput, CacheOutput, Empty, CacheFunctions> db;

        public MsFasterFetchCoin()
            : base(new BitcoinMain())
        {
        }

        [GlobalSetup]
        public void GenerateData()
        {
            Random rnd = new Random();
            this.data = new uint256[DataSize];
            for (int i = 0; i < DataSize; i++)
            {
                this.data[i] = new uint256((ulong)i);
            }

            Array.Sort(this.data, new UInt256Comparer());

            var result = this.CreateFasterLog();
            this.db = result.db;
            this.log = result.log;
            this.PopulateDB();
        }

        private void PopulateDB()
        {
            this.db.StartSession();

            var blockHashKey = new CacheKey("BlockHash", string.Empty);
            var blockHashValue = new CacheValue(this.data[0].ToBytes());
            this.db.Upsert(ref blockHashKey, ref blockHashValue, Empty.Default, 1L);

            foreach (uint256 d in this.data)
            {
                var coinKey = new CacheKey("Coins", d.ToString());
                var coinValue = new CacheValue(this.data[0].ToBytes());
                this.db.Upsert(ref coinKey, ref coinValue, Empty.Default, 1L);
            }

            this.db.Log.Flush(true);
            this.db.StopSession();
        }

        [Benchmark(Baseline = true)]
        public List<uint256> FetchData()
        {
            this.db.StartSession();

            var result = new List<uint256>(this.N);

            foreach (uint256 input in this.data.OrderBy(d => Guid.NewGuid()).Take(this.N))
            {
                var coinKey = new CacheKey("Coins", input.ToString());
                var output = new CacheOutput();
                var dbInput = default(CacheInput);
                var status = this.db.Read(ref coinKey, ref dbInput, ref output, Empty.Default, 1);
                if (status == Status.PENDING) this.db.CompletePending(true);

                uint256 outputs = status == Status.OK ? this.dBreezeSerializer.Deserialize<uint256>(output.Value.Value) : null;

                result.Add(outputs);
            }

            this.db.StopSession();

            return result;
        }

        [Benchmark]
        public void DeleteData()
        {
            this.db.StartSession();

            foreach (uint256 input in this.data.OrderBy(d => Guid.NewGuid()).Take(this.N))
            {
                var coinKey = new CacheKey("Coins", input.ToString());
                var dbInput = default(CacheInput);
                var status = this.db.DeleteFromMemory(ref coinKey, 1);
                if (status == Status.ERROR)
                    throw new ApplicationException("Failed to delete data");
            }

            this.db.StopSession();
        }
    }
}
