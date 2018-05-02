using System;
using System.Collections.Generic;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace NBitcoin
{
    public class NetworkBuilder
    {
        internal string Name;
        internal string RootFolderName;
        internal string DefaultConfigFilename;
        internal Dictionary<Base58Type, byte[]> Base58Prefixes;
        internal Dictionary<Bech32Type, Bech32Encoder> Bech32Prefixes;
        internal List<string> Aliases;
        internal int RPCPort;
        internal int Port;
        internal uint Magic;
        internal Consensus Consensus;
        internal List<DNSSeedData> Seeds;
        internal List<NetworkAddress> FixedSeeds;
        internal Block Genesis;
        internal long MinTxFee;
        internal int MaxTimeOffsetSeconds;
        internal int MaxTipAge;
        internal long FallbackFee;
        internal long MinRelayTxFee;
        internal Dictionary<int, CheckpointInfo> Checkpoints;

        public NetworkBuilder()
        {
            this.Base58Prefixes = new Dictionary<Base58Type, byte[]>();
            this.Bech32Prefixes = new Dictionary<Bech32Type, Bech32Encoder>();
            this.Aliases = new List<string>();
            this.Seeds = new List<DNSSeedData>();
            this.FixedSeeds = new List<NetworkAddress>();
            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
        }

        public NetworkBuilder SetName(string name)
        {
            this.Name = name;
            return this;
        }

        /// <summary>
        /// Sets the name of the folder containing the different blockchains.
        /// </summary>
        /// <param name="rootFolderName">The name of the folder.</param>
        /// <returns>A <see cref="NetworkBuilder"/>.</returns>
        public NetworkBuilder SetRootFolderName(string rootFolderName)
        {
            this.RootFolderName = rootFolderName;
            return this;
        }

        /// <summary>
        /// Sets the default name used for the network configuration file.
        /// </summary>
        /// <param name="defaultConfigFilename">The name of the file.</param>
        /// <returns>A <see cref="NetworkBuilder"/>.</returns>
        public NetworkBuilder SetDefaultConfigFilename(string defaultConfigFilename)
        {
            this.DefaultConfigFilename = defaultConfigFilename;
            return this;
        }

        public NetworkBuilder SetTxFees(long minTxFee, long fallbackFee, long minRelayTxFee)
        {
            this.MinTxFee = minTxFee;
            this.FallbackFee = fallbackFee;
            this.MinRelayTxFee = minRelayTxFee;
            return this;
        }

        /// <summary>
        /// Sets the maximal value allowed for the calculated time offset.
        /// </summary>
        /// <param name="maxTimeOffsetSeconds"> The maximal value allowed for the calculated time offset.</param>
        /// <returns>A <see cref="NetworkBuilder"/>.</returns>
        public NetworkBuilder SetMaxTimeOffsetSeconds(int maxTimeOffsetSeconds)
        {
            this.MaxTimeOffsetSeconds = maxTimeOffsetSeconds;
            return this;
        }

        /// <summary>
        /// Sets the maximum tip age in seconds to consider node in initial block download.
        /// </summary>
        /// <param name="maxTipAge">Maximum tip age in seconds to consider node in initial block download.</param>
        /// <returns>A <see cref="NetworkBuilder"/>.</returns>
        public NetworkBuilder SetMaxTipAge(int maxTipAge)
        {
            this.MaxTipAge = maxTipAge;
            return this;
        }

        public void CopyFrom(Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            this.Base58Prefixes.Clear();
            this.Bech32Prefixes.Clear();

            for (int i = 0; i < network.base58Prefixes.Length; i++)
                this.SetBase58Bytes((Base58Type)i, network.base58Prefixes[i]);

            for (int i = 0; i < network.bech32Encoders.Length; i++)
                this.SetBech32((Bech32Type)i, network.bech32Encoders[i]);

            this.SetConsensus(network.Consensus)
                .SetCheckpoints(network.Checkpoints)
                .SetGenesis(network.GetGenesis())
                .SetMagic(this.Magic)
                .SetPort(network.DefaultPort)
                .SetRPCPort(network.RPCPort)
                .SetTxFees(network.MinTxFee, network.FallbackFee, network.MinRelayTxFee)
                .SetMaxTimeOffsetSeconds(network.MaxTimeOffsetSeconds)
                .SetMaxTipAge(network.MaxTipAge);
        }

        public NetworkBuilder AddAlias(string alias)
        {
            this.Aliases.Add(alias);
            return this;
        }

        public NetworkBuilder SetRPCPort(int port)
        {
            this.RPCPort = port;
            return this;
        }

        public NetworkBuilder SetPort(int port)
        {
            this.Port = port;
            return this;
        }

        public NetworkBuilder SetMagic(uint magic)
        {
            this.Magic = magic;
            return this;
        }

        public NetworkBuilder AddDNSSeeds(IEnumerable<DNSSeedData> seeds)
        {
            this.Seeds.AddRange(seeds);
            return this;
        }
        public NetworkBuilder AddSeeds(IEnumerable<NetworkAddress> seeds)
        {
            this.FixedSeeds.AddRange(seeds);
            return this;
        }

        public NetworkBuilder SetConsensus(Consensus consensus)
        {
            this.Consensus = consensus?.Clone();
            return this;
        }

        public NetworkBuilder SetGenesis(Block genesis)
        {
            this.Genesis = genesis;
            return this;
        }

        public NetworkBuilder SetBase58Bytes(Base58Type type, byte[] bytes)
        {
            this.Base58Prefixes.AddOrReplace(type, bytes);
            return this;
        }

        public NetworkBuilder SetBech32(Bech32Type type, string humanReadablePart)
        {
            this.Bech32Prefixes.AddOrReplace(type, Encoders.Bech32(humanReadablePart));
            return this;
        }
        public NetworkBuilder SetBech32(Bech32Type type, Bech32Encoder encoder)
        {
            this.Bech32Prefixes.AddOrReplace(type, encoder);
            return this;
        }

        public NetworkBuilder SetCheckpoints(Dictionary<int, CheckpointInfo> checkpoints)
        {
            if (checkpoints == null)
                throw new ArgumentNullException("checkpoints");

            this.Checkpoints = checkpoints;
            return this;
        }

        /// <summary>
        /// Create an immutable Network instance, and register it globally so it is queriable through Network.GetNetwork(string name) and Network.GetNetworks().
        /// </summary>
        /// <returns></returns>
        public Network BuildAndRegister()
        {
            return Network.Register(this);
        }
    }
}
