using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Networks;
using Transaction = DBreeze.Transactions.Transaction;

namespace Stratis.Benchmark.Persistence.DBreeze
{
    [RankColumn]
    public class DBreezeFetchCoin : NetworkBenchmarkBase
    {
        [Params(10, 100)]
        public int N;

        private uint256[] data;

        public DBreezeFetchCoin()
            : base(new BitcoinMain())
        {
        }

        [GlobalSetup]
        public void GenerateData()
        {
            Random rnd = new Random();
            this.data = new uint256[this.N];
            for (int i = 0; i < this.N; i++)
            {
                this.data[i] = new uint256((ulong)rnd.Next());
            }
            Array.Sort(this.data, new UInt256Comparer());

            PopulateDB();
        }

        private DBreezeEngine GetDBEngine()
        {
            return this.CreateDBreezeEngine(this.N.ToString());
        }

        private void PopulateDB()
        {
            using (var engine = this.GetDBEngine())
            {
                // Store data.
                using (Transaction tx = engine.GetTransaction())
                {
                    tx.Insert("BlockHash", new byte[0], this.data[0].ToBytes());

                    foreach (uint256 d in this.data)
                    {
                        tx.Insert("Coins", d.ToBytes(false), d.ToBytes());
                    }

                    tx.Commit();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public List<uint256> FetchData()
        {
            using (var engine = this.GetDBEngine())
            {
                using (Transaction transaction = engine.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    var result = new List<uint256>(this.data.Length);

                    foreach (uint256 input in this.data)
                    {
                        Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>("Coins", input.ToBytes(false));
                        uint256 outputs = row.Exists ? this.dBreezeSerializer.Deserialize<uint256>(row.Value) : null;

                        result.Add(outputs);
                    }

                    return result;
                }
            }
        }

        [Benchmark]
        public List<uint256> ParallelRangePartitioner()
        {
            using (var engine = this.GetDBEngine())
            {
                var result = new List<uint256>(this.data.Length);
                var rangePartitioner = Partitioner.Create(0, this.data.Length);
                Parallel.ForEach(rangePartitioner, (range, loopState) =>
                {
                    using (Transaction transaction = engine.GetTransaction())
                    {
                        transaction.ValuesLazyLoadingIsOn = false;

                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>("Coins", this.data[i].ToBytes(false));
                            uint256 outputs = row.Exists ? this.dBreezeSerializer.Deserialize<uint256>(row.Value) : null;

                            result.Add(outputs);
                        }
                    }
                });

                return result;
            }
        }

        [Benchmark]
        public List<uint256> FetchAllRawDataThenParallelDeserialize()
        {
            using (var engine = this.GetDBEngine())
            {
                using (Transaction transaction = engine.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    var result = new uint256[this.data.Length];
                    var rawValues = new List<byte[]>(this.data.Length);

                    foreach (uint256 input in this.data)
                    {
                        Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>("Coins", input.ToBytes(false));

                        rawValues.Add(row.Value);
                    }

                    return rawValues.AsParallel().Select(rawValue => this.dBreezeSerializer.Deserialize<uint256>(rawValue)).ToList();
                }
            }
        }

        [Benchmark]
        public List<uint256> FetchAllRawDataThenParallelDeserializeWithPartitioner()
        {
            using (var engine = this.GetDBEngine())
            {
                using (Transaction transaction = engine.GetTransaction())
                {
                    transaction.ValuesLazyLoadingIsOn = false;

                    var result = new uint256[this.data.Length];
                    var rawValues = new List<byte[]>(this.data.Length);

                    foreach (uint256 input in this.data)
                    {
                        Row<byte[], byte[]> row = transaction.Select<byte[], byte[]>("Coins", input.ToBytes(false));

                        rawValues.Add(row.Value);
                    }

                    object innerLock = new object();
                    var results = new List<uint256>(this.data.Length);

                    var rangePartitioner = Partitioner.Create(0, this.data.Length);
                    Parallel.ForEach(rangePartitioner, (range, loopState) =>
                    {
                        List<uint256> partialResult = new List<uint256>(range.Item2 - range.Item1);
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            partialResult.Add(this.dBreezeSerializer.Deserialize<uint256>(rawValues[i]));
                        }

                        lock (innerLock)
                        {
                            results.AddRange(partialResult);
                        }
                    });

                    return results;
                }
            }
        }
    }
}
