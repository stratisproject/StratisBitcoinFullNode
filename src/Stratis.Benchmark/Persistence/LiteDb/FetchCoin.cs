namespace Stratis.Benchmark.Persistence.LiteDb
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using BenchmarkDotNet.Attributes;
    using Bitcoin.Networks;
    using LiteDB;
    using NBitcoin;
    using Stratis.Benchmark.Infrastructure.LiteDb;
    using Stratis.Bitcoin.Features.Consensus;

    [RankColumn]
    public class LiteDbFetchCoin : NetworkBenchmarkBase
    {
        [Params(1000)]
        public int N;

        private uint256[] data;

        public LiteDbFetchCoin() : base(new BitcoinMain())
        {
        }

        [GlobalSetup]
        public void GenerateData()
        {
            Random rnd = new Random();
            this.data = new uint256[DataSize];
            for (int i = 0; i < DataSize; i++)
            {
                this.data[i] = new uint256((ulong) i);
            }

            Array.Sort(this.data, new UInt256Comparer());

            this.PopulateDB();
        }

        private LiteDatabase GetDBEngine()
        {
            var db = this.CreateLiteDb(this.N.ToString());
            return db;
        }

        private void PopulateDB()
        {
            using (var engine = this.GetDBEngine())
            {
                engine.DropCollection("blockhash");
                engine.DropCollection("coins");
                var blockHash = engine.GetCollection<CacheRecord>("blockhash");
                var record = new CacheRecord { Id = Guid.NewGuid().ToString(), Value = this.data[0].ToBytes() };

                blockHash.Upsert(record);

                var coins = engine.GetCollection("coins");
                var mapper = BsonMapper.Global;
                mapper.Entity<CacheCoin>().Id(p => p.Id);

                var coinData = this.data.Select(d => mapper.ToDocument(new CacheCoin { Id = d.ToString(), Value = d.ToBytes() })).ToList();
                coins.InsertBulk(coinData);
            }
        }


        [Benchmark(Baseline = true)]
        public List<uint256> FetchData()
        {
            using (var engine = this.GetDBEngine())
            {
                var result = new List<uint256>(this.N);

                var collection = engine.GetCollection<CacheCoin>("coins");
                foreach (uint256 input in this.data.OrderBy(d => Guid.NewGuid()).Take(this.N))
                {
                    var coin = collection.FindById(input.ToString());
                    uint256 outputs = coin != null ? this.dBreezeSerializer.Deserialize<uint256>(coin.Value) : null;

                    result.Add(outputs);
                }

                return result;
            }
        }
        
        [Benchmark]
        public void DeleteData()
        {
            using (var engine = this.GetDBEngine())
            {
                var collection = engine.GetCollection<CacheCoin>("coins");
                foreach (uint256 input in this.data.OrderBy(d => Guid.NewGuid()).Take(this.N))
                {
                    var success = collection.Delete(input.ToString());
                    if (!success)
                        throw new ApplicationException("Failed to delete data");
                }
            }
        }
    }
}