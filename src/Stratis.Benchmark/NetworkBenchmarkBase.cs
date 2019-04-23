using DBreeze;
using FASTER.core;
using LiteDB;
using NBitcoin;
using Stratis.Benchmark.Infrastructure.Faster;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Benchmark
{
    /// <summary>
    /// Base class to use as Network benchmark.
    /// </summary>
    public abstract class NetworkBenchmarkBase
    {
        protected readonly string dataFolder;
        protected readonly Network network;
        protected readonly DBreezeSerializer dBreezeSerializer;
        protected const int DataSize = 1_000_000;

        public NetworkBenchmarkBase(Network network)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.dBreezeSerializer = new DBreezeSerializer(this.network.Consensus.ConsensusFactory);

            this.dataFolder = System.IO.Path.GetDirectoryName(this.GetType().Assembly.Location);
        }

        protected DBreezeEngine CreateDBreezeEngine(string DBName = "DB")
        {
            return new DBreezeEngine($"{this.dataFolder}/{this.GetType().Name}{DBName}");
        }

        protected LiteDatabase CreateLiteDb(string DBName = "DB")
        {
            return new LiteDatabase($"FileName={this.dataFolder}/{this.GetType().Name}{DBName}.db;Mode=Exclusive;");
        }

        protected (IDevice log, FasterKV<CacheKey, CacheValue, CacheInput, CacheOutput, Empty, CacheFunctions> db) CreateFasterLog()
        {
            var logSize = 1L << 20;
            var log = Devices.CreateLogDevice($"{this.dataFolder}/{this.GetType().Name}-hlog.log");

            var fht = new FasterKV
                <CacheKey, CacheValue, CacheInput, CacheOutput, Empty, CacheFunctions>(
                    logSize, new CacheFunctions(), new LogSettings { LogDevice = log },
                    null,
                    new SerializerSettings<CacheKey, CacheValue> { keySerializer = () => new CacheKeySerializer(), valueSerializer = () => new CacheValueSerializer() }
                );

            return (log, fht);
        }
    }
}