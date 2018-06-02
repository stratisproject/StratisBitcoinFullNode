using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Stealth;

namespace NBitcoin
{
    public enum Base58Type
    {
        PUBKEY_ADDRESS,
        SCRIPT_ADDRESS,
        SECRET_KEY,
        EXT_PUBLIC_KEY,
        EXT_SECRET_KEY,
        ENCRYPTED_SECRET_KEY_EC,
        ENCRYPTED_SECRET_KEY_NO_EC,
        PASSPHRASE_CODE,
        CONFIRMATION_CODE,
        STEALTH_ADDRESS,
        ASSET_ID,
        COLORED_ADDRESS,
        MAX_BASE58_TYPES,
    };

    public enum Bech32Type
    {
        WITNESS_PUBKEY_ADDRESS,
        WITNESS_SCRIPT_ADDRESS
    }

    public abstract partial class Network
    {
        private static readonly ConcurrentDictionary<string, Network> NetworksContainer = new ConcurrentDictionary<string, Network>();

        protected Block Genesis;
        
        protected Network()
        {
            this.Consensus = new Consensus();
        }

        /// <summary>
        /// The public key used in the alert messaging system.
        /// TODO: remove as per https://github.com/bitcoin/bitcoin/pull/7692.
        /// </summary>
        public PubKey AlertPubKey { get; protected set; }

        /// <summary>
        /// Maximal value for the calculated time offset.
        /// If the value is over this limit, the time syncing feature will be switched off.
        /// </summary>
        public int MaxTimeOffsetSeconds { get; protected set; }

        /// <summary>
        /// Maximum tip age in seconds to consider node in initial block download.
        /// </summary>
        public int MaxTipAge { get; protected set; }

        /// <summary>
        /// Mininum fee rate for all transactions.
        /// Fees smaller than this are considered zero fee for transaction creation.
        /// </summary>
        public long MinTxFee { get; protected set; }

        /// <summary>
        /// A fee rate that will be used when fee estimation has insufficient data.
        /// </summary>
        public long FallbackFee { get; protected set; }

        /// <summary>
        /// The minimum fee under which transactions may be rejected from being relayed.
        /// </summary>
        public long MinRelayTxFee { get; protected set; }

        /// <summary>
        /// Port on which to listen for incoming RPC connections.
        /// </summary>
        public int RPCPort { get; protected set; }

        /// <summary>
        /// The default port on which nodes of this network communicate with external clients. 
        /// </summary>
        public int DefaultPort { get; protected set; }

        /// <summary>
        /// The consensus for this network.
        /// </summary>
        public Consensus Consensus { get; protected set; }

        /// <summary>
        /// The name of the network.
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// A list of additional names the network can be referred as.
        /// For example, Bitcoin Main can have "Mainnet" as an additional name.
        /// </summary>
        public List<string> AdditionalNames { get; protected set; }

        /// <summary>
        /// The name of the root folder containing blockchains operating with the same consensus rules (for now, this will be bitcoin or stratis).
        /// </summary>
        public string RootFolderName { get; protected set; }

        /// <summary>
        /// The default name used for the network configuration file.
        /// </summary>
        public string DefaultConfigFilename { get; protected set; }

        /// <summary>
        /// The list of nodes on the network that our current node tries to connect to.
        /// </summary>
        public List<NetworkAddress> SeedNodes { get; protected set; }

        /// <summary>
        /// The list of DNS seeds from which to get IP addresses when bootstrapping a node.
        /// </summary>
        public List<DNSSeedData> DNSSeeds { get; protected set; }

        /// <summary>
        /// A list of well-known block hashes.
        /// The node considers all transactions and blocks up to these checkpoints as valid and irreversible.
        /// </summary>
        public Dictionary<int, CheckpointInfo> Checkpoints { get; protected set; }

        /// <summary>
        /// List of prefixes used in Base58 addresses.
        /// </summary>
        public byte[][] Base58Prefixes { get; protected set; }

        /// <summary>
        /// A list of Bech32 encoders.
        /// </summary>
        public Bech32Encoder[] Bech32Encoders { get; protected set; }

        /// <summary>
        /// A number used to identify the network.
        /// The message start string is designed to be unlikely to occur in normal data.
        /// The characters are rarely used upper ascii, not valid as UTF-8, and produce
        /// a large 4-byte int at any alignment.
        /// </summary>
        public uint Magic { get; protected set; }

        /// <summary>
        /// Byte array representation of a magic number.
        /// </summary>
        public byte[] MagicBytesArray;

        /// <summary>
        /// Byte representation of a magic number.
        /// Uses <see cref="Magic"/> if <see cref="MagicBytesArray"/> is null.
        /// TODO: Merge these 3 magic properties into fewer.
        /// </summary>
        public byte[] MagicBytes
        {
            get
            {
                if (this.MagicBytesArray == null)
                {
                    var bytes = new byte[]
                    {
                        (byte) this.Magic,
                        (byte) (this.Magic >> 8),
                        (byte) (this.Magic >> 16),
                        (byte) (this.Magic >> 24)
                    };
                    this.MagicBytesArray = bytes;
                }

                return this.MagicBytesArray;
            }
        }

        /// <summary>
        /// The UNIX time at inception of the genesis block for this network.
        /// </summary>
        public uint GenesisTime { get; protected set; }

        /// <summary>
        /// A hash which proves that a sufficient amount of computation has been carried out to create the genesis block.
        /// </summary>
        public uint GenesisNonce { get; protected set; }

        /// <summary>
        /// Represents the encoded form of the target threshold as it appears in the block header.
        /// </summary>
        public uint GenesisBits { get; protected set; }

        /// <summary>
        /// The version of the genesis block.
        /// </summary>
        public int GenesisVersion { get; protected set; }

        /// <summary>
        /// The reward for the genesis block, which is unspendable.
        /// </summary>
        public Money GenesisReward { get; protected set; }

        /// <summary>
        /// Register an immutable <see cref="Network"/> instance so it is queryable through <see cref="GetNetwork(string)"/> and <see cref="GetNetworks()"/>.
        /// </summary>
        internal static Network Register(Network network)
        {
            IEnumerable<string> networkNames = network.AdditionalNames != null ? new[] { network.Name }.Concat(network.AdditionalNames) : new[] { network.Name };

            foreach (string networkName in networkNames)
            {
                // Performs a series of checks before registering the network to the list of available networks.
                if (string.IsNullOrEmpty(networkName))
                    throw new InvalidOperationException("A network name needs to be provided.");

                if (GetNetwork(networkName) != null)
                    throw new InvalidOperationException("The network " + networkName + " is already registered.");

                if (network.GetGenesis() == null)
                    throw new InvalidOperationException("A genesis block needs to be provided.");

                if (network.Consensus == null)
                    throw new InvalidOperationException("A consensus needs to be provided.");

                NetworksContainer.TryAdd(networkName.ToLowerInvariant(), network);
            }

            return network;
        }

        protected static void Assert(bool condition)
        {
            // TODO: use Guard when this moves to the FN.
            if (!condition)
            {
                throw new InvalidOperationException("Invalid network");
            }
        }

        public BitcoinSecret CreateBitcoinSecret(string base58)
        {
            return new BitcoinSecret(base58, this);
        }

        /// <summary>
        /// Create a bitcoin address from base58 data, return a BitcoinAddress or BitcoinScriptAddress
        /// </summary>
        /// <param name="base58">base58 address</param>
        /// <exception cref="System.FormatException">Invalid base58 address</exception>
        /// <returns>BitcoinScriptAddress, BitcoinAddress</returns>
        public BitcoinAddress CreateBitcoinAddress(string base58)
        {
            Base58Type? type = GetBase58Type(base58);
            if (!type.HasValue)
                throw new FormatException("Invalid Base58 version");
            if (type == Base58Type.PUBKEY_ADDRESS)
                return this.CreateBitcoinPubKeyAddress(base58);
            if (type == Base58Type.SCRIPT_ADDRESS)
                return this.CreateBitcoinScriptAddress(base58);
            throw new FormatException("Invalid Base58 version");
        }

        private Base58Type? GetBase58Type(string base58)
        {
            var bytes = Encoders.Base58Check.DecodeData(base58);
            for (int i = 0; i < this.Base58Prefixes.Length; i++)
            {
                var prefix = this.Base58Prefixes[i];
                if (prefix == null)
                    continue;
                if (bytes.Length < prefix.Length)
                    continue;
                if (Utils.ArrayEqual(bytes, 0, prefix, 0, prefix.Length))
                    return (Base58Type) i;
            }
            return null;
        }

        internal static Network GetNetworkFromBase58Data(string base58, Base58Type? expectedType = null)
        {
            foreach (Network network in GetNetworks())
            {
                Base58Type? type = network.GetBase58Type(base58);
                if (type.HasValue)
                {
                    if (expectedType != null && expectedType.Value != type.Value)
                        continue;
                    if (type.Value == Base58Type.COLORED_ADDRESS)
                    {
                        var raw = Encoders.Base58Check.DecodeData(base58);
                        var version = network.GetVersionBytes(type.Value, false);
                        if (version == null)
                            continue;
                        raw = raw.Skip(version.Length).ToArray();
                        base58 = Encoders.Base58Check.EncodeData(raw);
                        return GetNetworkFromBase58Data(base58, null);
                    }
                    return network;
                }
            }
            return null;
        }

        public IBitcoinString Parse(string str)
        {
            return Parse<IBitcoinString>(str, this);
        }

        public T Parse<T>(string str) where T : IBitcoinString
        {
            return Parse<T>(str, this);
        }

        public static IBitcoinString Parse(string str, Network expectedNetwork)
        {
            return Parse<IBitcoinString>(str, expectedNetwork);
        }

        public static T Parse<T>(string str, Network expectedNetwork) where T : IBitcoinString
        {
            if (str == null)
                throw new ArgumentNullException("str");

            IEnumerable<Network> networks = expectedNetwork == null ? GetNetworks() : new[] {expectedNetwork};
            var maybeb58 = true;
            for (int i = 0; i < str.Length; i++)
            {
                if (!Base58Encoder.pszBase58Chars.Contains(str[i]))
                {
                    maybeb58 = false;
                    break;
                }
            }

            if (maybeb58)
            {
                try
                {
                    Encoders.Base58Check.DecodeData(str);
                }
                catch (FormatException)
                {
                    maybeb58 = false;
                }
                if (maybeb58)
                {
                    foreach (IBase58Data candidate in GetCandidates(networks, str))
                    {
                        bool rightNetwork = expectedNetwork == null || (candidate.Network == expectedNetwork);
                        bool rightType = candidate is T;
                        if (rightNetwork && rightType)
                            return (T) (object) candidate;
                    }
                    throw new FormatException("Invalid base58 string");
                }
            }

            foreach (Network network in networks)
            {
                int i = -1;
                foreach (Bech32Encoder encoder in network.Bech32Encoders)
                {
                    i++;
                    if (encoder == null)
                        continue;
                    var type = (Bech32Type) i;
                    try
                    {
                        byte witVersion;
                        var bytes = encoder.Decode(str, out witVersion);
                        object candidate = null;

                        if (witVersion == 0 && bytes.Length == 20 && type == Bech32Type.WITNESS_PUBKEY_ADDRESS)
                            candidate = new BitcoinWitPubKeyAddress(str, network);
                        if (witVersion == 0 && bytes.Length == 32 && type == Bech32Type.WITNESS_SCRIPT_ADDRESS)
                            candidate = new BitcoinWitScriptAddress(str, network);

                        if (candidate is T)
                            return (T) (object) candidate;
                    }
                    catch (Bech32FormatException)
                    {
                        throw;
                    }
                    catch (FormatException)
                    {
                        continue;
                    }
                }

            }

            throw new FormatException("Invalid string");
        }

        private static IEnumerable<IBase58Data> GetCandidates(IEnumerable<Network> networks, string base58)
        {
            if (base58 == null)
                throw new ArgumentNullException("base58");
            foreach (Network network in networks)
            {
                Base58Type? type = network.GetBase58Type(base58);
                if (type.HasValue)
                {
                    if (type.Value == Base58Type.COLORED_ADDRESS)
                    {
                        var wrapped = BitcoinColoredAddress.GetWrappedBase58(base58, network);
                        Base58Type? wrappedType = network.GetBase58Type(wrapped);
                        if (wrappedType == null)
                            continue;
                        try
                        {
                            IBase58Data inner = network.CreateBase58Data(wrappedType.Value, wrapped);
                            if (inner.Network != network)
                                continue;
                        }
                        catch (FormatException)
                        {
                        }
                    }
                    IBase58Data data = null;
                    try
                    {
                        data = network.CreateBase58Data(type.Value, base58);
                    }
                    catch (FormatException)
                    {
                    }
                    if (data != null)
                        yield return data;
                }
            }
        }

        private IBase58Data CreateBase58Data(Base58Type type, string base58)
        {
            if (type == Base58Type.EXT_PUBLIC_KEY)
                return this.CreateBitcoinExtPubKey(base58);
            if (type == Base58Type.EXT_SECRET_KEY)
                return this.CreateBitcoinExtKey(base58);
            if (type == Base58Type.PUBKEY_ADDRESS)
                return this.CreateBitcoinPubKeyAddress(base58);
            if (type == Base58Type.SCRIPT_ADDRESS)
                return this.CreateBitcoinScriptAddress(base58);
            if (type == Base58Type.SECRET_KEY)
                return this.CreateBitcoinSecret(base58);
            if (type == Base58Type.CONFIRMATION_CODE)
                return this.CreateConfirmationCode(base58);
            if (type == Base58Type.ENCRYPTED_SECRET_KEY_EC)
                return this.CreateEncryptedKeyEC(base58);
            if (type == Base58Type.ENCRYPTED_SECRET_KEY_NO_EC)
                return this.CreateEncryptedKeyNoEC(base58);
            if (type == Base58Type.PASSPHRASE_CODE)
                return this.CreatePassphraseCode(base58);
            if (type == Base58Type.STEALTH_ADDRESS)
                return this.CreateStealthAddress(base58);
            if (type == Base58Type.ASSET_ID)
                return this.CreateAssetId(base58);
            if (type == Base58Type.COLORED_ADDRESS)
                return this.CreateColoredAddress(base58);
            throw new NotSupportedException("Invalid Base58Data type : " + type.ToString());
        }

        public BitcoinScriptAddress CreateBitcoinScriptAddress(string base58)
        {
            return new BitcoinScriptAddress(base58, this);
        }

        private BitcoinColoredAddress CreateColoredAddress(string base58)
        {
            return new BitcoinColoredAddress(base58, this);
        }

        public NBitcoin.OpenAsset.BitcoinAssetId CreateAssetId(string base58)
        {
            return new NBitcoin.OpenAsset.BitcoinAssetId(base58, this);
        }

        public BitcoinStealthAddress CreateStealthAddress(string base58)
        {
            return new BitcoinStealthAddress(base58, this);
        }

        private BitcoinPassphraseCode CreatePassphraseCode(string base58)
        {
            return new BitcoinPassphraseCode(base58, this);
        }

        private BitcoinEncryptedSecretNoEC CreateEncryptedKeyNoEC(string base58)
        {
            return new BitcoinEncryptedSecretNoEC(base58, this);
        }

        private BitcoinEncryptedSecretEC CreateEncryptedKeyEC(string base58)
        {
            return new BitcoinEncryptedSecretEC(base58, this);
        }

        private Base58Data CreateConfirmationCode(string base58)
        {
            return new BitcoinConfirmationCode(base58, this);
        }

        private Base58Data CreateBitcoinExtPubKey(string base58)
        {
            return new BitcoinExtPubKey(base58, this);
        }

        public BitcoinExtKey CreateBitcoinExtKey(ExtKey key)
        {
            return new BitcoinExtKey(key, this);
        }

        public BitcoinExtPubKey CreateBitcoinExtPubKey(ExtPubKey pubkey)
        {
            return new BitcoinExtPubKey(pubkey, this);
        }

        public BitcoinExtKey CreateBitcoinExtKey(string base58)
        {
            return new BitcoinExtKey(base58, this);
        }

        public BitcoinSecret CreateBitcoinSecret(Key key)
        {
            return new BitcoinSecret(key, this);
        }

        public BitcoinPubKeyAddress CreateBitcoinPubKeyAddress(KeyId dest)
        {
            return new BitcoinPubKeyAddress(dest, this);
        }

        public BitcoinPubKeyAddress CreateBitcoinPubKeyAddress(string base58)
        {
            return new BitcoinPubKeyAddress(base58, this);
        }

        public override string ToString()
        {
            return this.Name;
        }

        public Block GetGenesis()
        {
            return this.Genesis.Clone(network: this);
        }

        public uint256 GenesisHash => this.Consensus.HashGenesisBlock;

        public static IEnumerable<Network> GetNetworks()
        {
            yield return Main;
            yield return TestNet;
            yield return RegTest;

            if (NetworksContainer.Any())
            {
                List<Network> others = NetworksContainer.Values.Distinct().ToList();

                foreach (Network network in others)
                    yield return network;
            }
        }

        /// <summary>
        /// Get network from name
        /// </summary>
        /// <param name="name">main,mainnet,testnet,test,testnet3,reg,regtest,seg,segnet</param>
        /// <returns>The network or null of the name does not match any network</returns>
        public static Network GetNetwork(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            if (NetworksContainer.Any())
            {
                name = name.ToLowerInvariant();
                return NetworksContainer.TryGet(name);
            }

            return null;
        }

        public Money GetReward(int nHeight)
        {
            long nSubsidy = new Money(50 * Money.COIN);
            int halvings = nHeight / this.Consensus.SubsidyHalvingInterval;

            // Force block reward to zero when right shift is undefined.
            if (halvings >= 64)
                return Money.Zero;

            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            nSubsidy >>= halvings;

            return new Money(nSubsidy);
        }

        public bool ReadMagic(Stream stream, CancellationToken cancellation, bool throwIfEOF = false)
        {
            byte[] bytes = new byte[1];
            for (int i = 0; i < this.MagicBytes.Length; i++)
            {
                i = Math.Max(0, i);
                cancellation.ThrowIfCancellationRequested();

                var read = stream.ReadEx(bytes, 0, bytes.Length, cancellation);
                if (read == 0)
                    if (throwIfEOF)
                        throw new EndOfStreamException("No more bytes to read");
                    else
                        return false;
                if (read != 1)
                    i--;
                else if (this.MagicBytesArray[i] != bytes[0])
                    i = this.MagicBytesArray[0] == bytes[0] ? 0 : -1;
            }
            return true;
        }

        public Bech32Encoder GetBech32Encoder(Bech32Type type, bool throws)
        {
            Bech32Encoder encoder = this.Bech32Encoders[(int)type];
            if (encoder == null && throws)
                throw new NotImplementedException("The network " + this + " does not have any prefix for bech32 " +
                                                  Enum.GetName(typeof(Bech32Type), type));
            return encoder;
        }

        public byte[] GetVersionBytes(Base58Type type, bool throws)
        {
            var prefix = this.Base58Prefixes[(int)type];
            if (prefix == null && throws)
                throw new NotImplementedException("The network " + this + " does not have any prefix for base58 " +
                                                  Enum.GetName(typeof(Base58Type), type));
            return prefix?.ToArray();
        }

        internal static string CreateBase58(Base58Type type, byte[] bytes, Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            var versionBytes = network.GetVersionBytes(type, true);
            return Encoders.Base58Check.EncodeData(versionBytes.Concat(bytes));
        }

        internal static string CreateBech32(Bech32Type type, byte[] bytes, byte witnessVersion, Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            Bech32Encoder encoder = network.GetBech32Encoder(type, true);
            return encoder.Encode(witnessVersion, bytes);
        }

        protected IEnumerable<NetworkAddress> ConvertToNetworkAddresses(string[] seeds, int defaultPort)
        {
            Random rand = new Random();
            TimeSpan oneWeek = TimeSpan.FromDays(7);

            foreach (string seed in seeds)
            {
                // It'll only connect to one or two seed nodes because once it connects,
                // it'll get a pile of addresses with newer timestamps.
                // Seed nodes are given a random 'last seen time' of between one and two weeks ago.
                yield return new NetworkAddress
                {
                    Time = DateTime.UtcNow - (TimeSpan.FromSeconds(rand.NextDouble() * oneWeek.TotalSeconds)) - oneWeek,
                    Endpoint = Utils.ParseIpEndpoint(seed, defaultPort)
                };
            }
        }
    }
}