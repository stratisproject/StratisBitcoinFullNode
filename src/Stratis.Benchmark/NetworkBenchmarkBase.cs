using System;
using System.Runtime.CompilerServices;
using DBreeze;
using NBitcoin;
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
    }
}