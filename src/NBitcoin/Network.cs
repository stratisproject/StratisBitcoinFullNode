using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Stealth;

namespace NBitcoin
{
    public class DNSSeedData
    {
        private IPAddress[] addresses;

        public string Name { get; }

        public string Host { get; }

        public DNSSeedData(string name, string host)
        {
            this.Name = name;
            this.Host = host;
        }

        public IPAddress[] GetAddressNodes()
        {
            if (this.addresses != null)
            {
                return this.addresses;
            }

            this.addresses = Dns.GetHostAddressesAsync(this.Host).GetAwaiter().GetResult();

            return this.addresses;
        }

        public override string ToString()
        {
            return $"{this.Name}({this.Host})";
        }
    }

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

    public enum BuriedDeployments : int
    {
        /// <summary>
        /// Height in coinbase
        /// </summary>
        BIP34,

        /// <summary>
        /// Height in OP_CLTV
        /// </summary>
        BIP65,

        /// <summary>
        /// Strict DER signature
        /// </summary>
        BIP66
    }

    public class Consensus
    {
        /// <summary>
        /// An extension to <see cref="Consensus"/> to enable additional options to the consensus data.
        /// </summary>
        public class ConsensusOptions
        {
        }

        public ConsensusOptions Options { get; set; }
        public NetworkOptions NetworkOptions { get; set; } = NetworkOptions.TemporaryOptions;

        public class BuriedDeploymentsArray
        {
            readonly Consensus parent;
            readonly int[] heights;

            public BuriedDeploymentsArray(Consensus parent)
            {
                this.parent = parent;
                this.heights = new int[Enum.GetValues(typeof(BuriedDeployments)).Length];
            }

            public int this[BuriedDeployments index]
            {
                get { return this.heights[(int)index]; }
                set { this.heights[(int)index] = value; }
            }
        }

        public class BIP9DeploymentsArray
        {
            readonly Consensus parent;
            readonly BIP9DeploymentsParameters[] parameters;

            public BIP9DeploymentsArray(Consensus parent)
            {
                this.parent = parent;
                this.parameters = new BIP9DeploymentsParameters[Enum.GetValues(typeof(BIP9Deployments)).Length];
            }

            public BIP9DeploymentsParameters this[BIP9Deployments index]
            {
                get { return this.parameters[(int) index]; }
                set { this.parameters[(int) index] = value; }
            }
        }

        public Consensus()
        {
            this.BuriedDeployments = new BuriedDeploymentsArray(this);
            this.BIP9Deployments = new BIP9DeploymentsArray(this);
        }

        public BuriedDeploymentsArray BuriedDeployments { get; }

        public BIP9DeploymentsArray BIP9Deployments { get; }

        public int SubsidyHalvingInterval { get; set; }

        public Func<NetworkOptions, BlockHeader, uint256> GetPoWHash { get; set; } = (n,h) => h.GetHash(n);

        public int MajorityEnforceBlockUpgrade { get; set; }

        public int MajorityRejectBlockOutdated { get; set; }

        public int MajorityWindow { get; set; }

        public uint256 BIP34Hash { get; set; }

        public Target PowLimit { get; set; }

        public TimeSpan PowTargetTimespan { get; set; }

        public TimeSpan PowTargetSpacing { get; set; }

        public bool PowAllowMinDifficultyBlocks { get; set; }

        public bool PowNoRetargeting { get; set; }

        public uint256 HashGenesisBlock { get; set; }

        public uint256 MinimumChainWork { get; set; }

        public long DifficultyAdjustmentInterval
        {
            get { return ((long) this.PowTargetTimespan.TotalSeconds / (long) this.PowTargetSpacing.TotalSeconds); }
        }

        public int MinerConfirmationWindow { get; set; }

        public int RuleChangeActivationThreshold { get; set; }

        /// <summary>
        /// Specify the BIP44 coin type for this network
        /// </summary>
        public int CoinType { get; set; }

        /// <summary>
        /// Specify using litecoin calculation for difficulty
        /// </summary>
        public bool LitecoinWorkCalculation { get; set; }

        public BigInteger ProofOfStakeLimit { get; set; }

        public BigInteger ProofOfStakeLimitV2 { get; set; }

        public int LastPOWBlock { get; set; }

        /// <summary>The default hash to use for assuming valid blocks.</summary>
        public uint256 DefaultAssumeValid { get; set; }

        public virtual Consensus Clone()
        {
            return new Consensus
            {
                BIP34Hash = this.BIP34Hash,
                HashGenesisBlock = this.HashGenesisBlock,
                MajorityEnforceBlockUpgrade = this.MajorityEnforceBlockUpgrade,
                MajorityRejectBlockOutdated = this.MajorityRejectBlockOutdated,
                MajorityWindow = this.MajorityWindow,
                MinerConfirmationWindow = this.MinerConfirmationWindow,
                PowAllowMinDifficultyBlocks = this.PowAllowMinDifficultyBlocks,
                PowLimit = this.PowLimit,
                PowNoRetargeting = this.PowNoRetargeting,
                PowTargetSpacing = this.PowTargetSpacing,
                PowTargetTimespan = this.PowTargetTimespan,
                RuleChangeActivationThreshold = this.RuleChangeActivationThreshold,
                SubsidyHalvingInterval = this.SubsidyHalvingInterval,
                MinimumChainWork = this.MinimumChainWork,
                GetPoWHash = this.GetPoWHash,
                CoinType = this.CoinType,
                LitecoinWorkCalculation = this.LitecoinWorkCalculation,
                LastPOWBlock = this.LastPOWBlock,
                ProofOfStakeLimit = this.ProofOfStakeLimit,
                ProofOfStakeLimitV2 = this.ProofOfStakeLimitV2,
                DefaultAssumeValid = this.DefaultAssumeValid,
                NetworkOptions = this.NetworkOptions.Clone()
            };
        }
    }

    public partial class Network
    {
        private uint magic;
        private byte[] alertPubKeyArray;
        private PubKey alertPubKey;
        private readonly List<DNSSeedData> seeds = new List<DNSSeedData>();
        private readonly List<NetworkAddress> fixedSeeds = new List<NetworkAddress>();
        private readonly Dictionary<int, CheckpointInfo> checkpoints = new Dictionary<int, CheckpointInfo>();
        private Block genesis;
        private Consensus consensus = new Consensus();

        public NetworkOptions NetworkOptions
        {
            get
            {
                return this.consensus.NetworkOptions;
            }
        }

        private Network()
        {
            this.genesis = new Block();
        }

        public PubKey AlertPubKey
        {
            get
            {
                if (this.alertPubKey == null)
                {
                    this.alertPubKey = new PubKey(this.alertPubKeyArray);
                }
                return this.alertPubKey;
            }
        }

        /// <summary> Maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public int MaxTimeOffsetSeconds { get; private set; }

        /// <summary>Maximum tip age in seconds to consider node in initial block download.</summary>
        public int MaxTipAge { get; private set; }

        public long MinTxFee { get; private set; }

        public long FallbackFee { get; private set; }

        public long MinRelayTxFee { get; private set; }

        public int RPCPort { get; private set; }

        public int DefaultPort { get; private set; }

        public Consensus Consensus => this.consensus;

        public string Name { get; private set; }

        /// <summary> The name of the root folder containing blockchains operating with the same consensus rules (for now, this will be bitcoin or stratis). </summary>
        public string RootFolderName { get; private set; }

        /// <summary> The default name used for the network configuration file. </summary>
        public string DefaultConfigFilename { get; private set; }

        public IEnumerable<NetworkAddress> SeedNodes => this.fixedSeeds;

        public IEnumerable<DNSSeedData> DNSSeeds => this.seeds;

        public Dictionary<int, CheckpointInfo> Checkpoints => this.checkpoints;

        public byte[] MagicBytesArray;

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

        public uint Magic => this.magic;

        static readonly ConcurrentDictionary<string, Network> NetworksContainer =
            new ConcurrentDictionary<string, Network>();

        internal static Network Register(NetworkBuilder builder)
        {
            if (builder.Name == null)
                throw new InvalidOperationException("A network name needs to be provided.");

            if (GetNetwork(builder.Name) != null)
                throw new InvalidOperationException("The network " + builder.Name + " is already registered.");

            if (builder.Genesis == null)
                throw new InvalidOperationException("A genesis block needs to be provided.");

            if (builder.Consensus == null)
                throw new InvalidOperationException("A consensus needs to be provided.");

            Network network = new Network();
            network.Name = builder.Name;
            network.RootFolderName = builder.RootFolderName;
            network.DefaultConfigFilename = builder.DefaultConfigFilename;
            network.consensus = builder.Consensus;
            network.magic = builder.Magic;
            network.DefaultPort = builder.Port;
            network.RPCPort = builder.RPCPort;
            network.genesis = builder.Genesis;
            network.consensus.HashGenesisBlock = network.genesis.GetHash();

            foreach (DNSSeedData seed in builder.Seeds)
            {
                network.seeds.Add(seed);
            }

            foreach (NetworkAddress seed in builder.FixedSeeds)
            {
                network.fixedSeeds.Add(seed);
            }

            network.base58Prefixes = Network.Main.base58Prefixes.ToArray();

            foreach (KeyValuePair<Base58Type, byte[]> kv in builder.Base58Prefixes)
            {
                network.base58Prefixes[(int) kv.Key] = kv.Value;
            }

            network.bech32Encoders = Network.Main.bech32Encoders.ToArray();

            foreach (KeyValuePair<Bech32Type, Bech32Encoder> kv in builder.Bech32Prefixes)
            {
                network.bech32Encoders[(int) kv.Key] = kv.Value;
            }

            foreach (string alias in builder.Aliases)
            {
                NetworksContainer.TryAdd(alias.ToLowerInvariant(), network);
            }

            NetworksContainer.TryAdd(network.Name.ToLowerInvariant(), network);

            network.MaxTimeOffsetSeconds = builder.MaxTimeOffsetSeconds;
            network.MaxTipAge = builder.MaxTipAge;
            network.MinTxFee = builder.MinTxFee;
            network.FallbackFee = builder.FallbackFee;
            network.MinRelayTxFee = builder.MinRelayTxFee;

            foreach (KeyValuePair<int, CheckpointInfo> checkpoint in builder.Checkpoints)
            {
                network.checkpoints.Add(checkpoint.Key, checkpoint.Value);
            }

            return network;
        }

        private static void Assert(bool condition)
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
                return new BitcoinPubKeyAddress(base58, this);
            if (type == Base58Type.SCRIPT_ADDRESS)
                return new BitcoinScriptAddress(base58, this);
            throw new FormatException("Invalid Base58 version");
        }

        public BitcoinScriptAddress CreateBitcoinScriptAddress(string base58)
        {
            return new BitcoinScriptAddress(base58, this);
        }

        private Base58Type? GetBase58Type(string base58)
        {
            var bytes = Encoders.Base58Check.DecodeData(base58);
            for (int i = 0; i < this.base58Prefixes.Length; i++)
            {
                var prefix = this.base58Prefixes[i];
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
                foreach (Bech32Encoder encoder in network.bech32Encoders)
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
                return new BitcoinPubKeyAddress(base58, this);
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

        public override string ToString()
        {
            return this.Name;
        }

        public Block GetGenesis()
        {
            return this.genesis.Clone(options:this.NetworkOptions);
        }

        public uint256 GenesisHash => this.consensus.HashGenesisBlock;

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
        /// Get network from protocol magic number
        /// </summary>
        /// <param name="magic">Magic number</param>
        /// <returns>The network, or null of the magic number does not match any network</returns>
        public static Network GetNetwork(uint magic)
        {
            return GetNetworks().FirstOrDefault(r => r.Magic == magic);
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

        public BitcoinSecret CreateBitcoinSecret(Key key)
        {
            return new BitcoinSecret(key, this);
        }

        public BitcoinPubKeyAddress CreateBitcoinAddress(KeyId dest)
        {
            if (dest == null)
                throw new ArgumentNullException("dest");
            return new BitcoinPubKeyAddress(dest, this);
        }

        private BitcoinAddress CreateBitcoinScriptAddress(ScriptId scriptId)
        {
            return new BitcoinScriptAddress(scriptId, this);
        }
        /*
        public Message ParseMessage(byte[] bytes, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            BitcoinStream bstream = new BitcoinStream(bytes);
            Message message = new Message();

            using (bstream.ProtocolVersionScope(version))
            {
                bstream.ReadWrite(ref message);
            }

            if (message.Magic != this.magic)
                throw new FormatException("Unexpected magic field in the message");

            return message;
        }
        */

        public Money GetReward(int nHeight)
        {
            long nSubsidy = new Money(50 * Money.COIN);
            int halvings = nHeight / this.consensus.SubsidyHalvingInterval;

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

        internal byte[][] base58Prefixes = new byte[12][];

        internal Bech32Encoder[] bech32Encoders = new Bech32Encoder[2];

        public Bech32Encoder GetBech32Encoder(Bech32Type type, bool throws)
        {
            Bech32Encoder encoder = this.bech32Encoders[(int)type];
            if (encoder == null && throws)
                throw new NotImplementedException("The network " + this + " does not have any prefix for bech32 " +
                                                  Enum.GetName(typeof(Bech32Type), type));
            return encoder;
        }

        public byte[] GetVersionBytes(Base58Type type, bool throws)
        {
            var prefix = this.base58Prefixes[(int)type];
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
    }
}