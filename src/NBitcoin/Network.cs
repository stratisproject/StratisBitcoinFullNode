using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using NBitcoin.Stealth;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace NBitcoin
{
    public class DNSSeedData
    {
        string name, host;
        public string Name
        {
            get
            {
                return name;
            }
        }
        public string Host
        {
            get
            {
                return host;
            }
        }
        public DNSSeedData(string name, string host)
        {
            this.name = name;
            this.host = host;
        }
        IPAddress[] _Addresses = null;
        public IPAddress[] GetAddressNodes()
        {
            if(_Addresses != null)
                return _Addresses;
            try
            {
                _Addresses = Dns.GetHostAddressesAsync(host).Result;
            }
            catch(AggregateException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            return _Addresses;
        }
        public override string ToString()
        {
            return name + " (" + host + ")";
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

    public partial class Network
    {
        internal byte[][] base58Prefixes = new byte[12][];
        internal Bech32Encoder[] bech32Encoders = new Bech32Encoder[2];
        public Bech32Encoder GetBech32Encoder(Bech32Type type, bool throws)
        {
            var encoder = bech32Encoders[(int)type];
            if(encoder == null && throws)
                throw new NotImplementedException("The network " + this + " does not have any prefix for bech32 " + Enum.GetName(typeof(Bech32Type), type));
            return encoder;
        }

        public byte[] GetVersionBytes(Base58Type type, bool throws)
        {
            var prefix = base58Prefixes[(int)type];
            if(prefix == null && throws)
                throw new NotImplementedException("The network " + this + " does not have any prefix for base58 " + Enum.GetName(typeof(Base58Type), type));
            return prefix?.ToArray();
        }

        internal static string CreateBase58(Base58Type type, byte[] bytes, Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            if(bytes == null)
                throw new ArgumentNullException("bytes");
            var versionBytes = network.GetVersionBytes(type, true);
            return Encoders.Base58Check.EncodeData(versionBytes.Concat(bytes));
        }

        internal static string CreateBech32(Bech32Type type, byte[] bytes, byte witnessVersion, Network network)
        {
            if(network == null)
                throw new ArgumentNullException("network");
            if(bytes == null)
                throw new ArgumentNullException("bytes");
            var encoder = network.GetBech32Encoder(type, true);
            return encoder.Encode(witnessVersion, bytes);
        }
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

        public class BuriedDeploymentsArray
        {
            Consensus _Parent;
            int[] _Heights;
            public BuriedDeploymentsArray(Consensus parent)
            {
                _Parent = parent;
                _Heights = new int[Enum.GetValues(typeof(BuriedDeployments)).Length];
            }
            public int this[BuriedDeployments index]
            {
                get
                {
                    return _Heights[(int)index];
                }
                set
                {
                    _Parent.EnsureNotFrozen();
                    _Heights[(int)index] = value;
                }
            }
        }
        public class BIP9DeploymentsArray
        {
            Consensus _Parent;
            BIP9DeploymentsParameters[] _Parameters;
            public BIP9DeploymentsArray(Consensus parent)
            {
                _Parent = parent;
                _Parameters = new BIP9DeploymentsParameters[Enum.GetValues(typeof(BIP9Deployments)).Length];
            }

            public BIP9DeploymentsParameters this[BIP9Deployments index]
            {
                get
                {
                    return _Parameters[(int)index];
                }
                set
                {
                    _Parent.EnsureNotFrozen();
                    _Parameters[(int)index] = value;
                }
            }
        }

        public Consensus()
        {
            this.buriedDeployments = new BuriedDeploymentsArray(this);
            this.bIP9Deployments = new BIP9DeploymentsArray(this);
        }
        private readonly BuriedDeploymentsArray buriedDeployments;
        public BuriedDeploymentsArray BuriedDeployments
        {
            get
            {
                return this.buriedDeployments;
            }
        }


        private readonly BIP9DeploymentsArray bIP9Deployments;
        public BIP9DeploymentsArray BIP9Deployments
        {
            get
            {
                return this.bIP9Deployments;
            }
        }

        int subsidyHalvingInterval;
        public int SubsidyHalvingInterval
        {
            get
            {
                return this.subsidyHalvingInterval;
            }
            set
            {
                EnsureNotFrozen();
                this.subsidyHalvingInterval = value;
            }
        }

        private Func<BlockHeader, uint256> getPoWHash = h => h.GetHash();

        public Func<BlockHeader, uint256> GetPoWHash
        {
            get
            {
                return this.getPoWHash;
            }
            set
            {
                EnsureNotFrozen();
                this.getPoWHash = value;
            }
        }


        int majorityEnforceBlockUpgrade;

        public int MajorityEnforceBlockUpgrade
        {
            get
            {
                return this.majorityEnforceBlockUpgrade;
            }
            set
            {
                EnsureNotFrozen();
                this.majorityEnforceBlockUpgrade = value;
            }
        }

        int majorityRejectBlockOutdated;
        public int MajorityRejectBlockOutdated
        {
            get
            {
                return this.majorityRejectBlockOutdated;
            }
            set
            {
                EnsureNotFrozen();
                this.majorityRejectBlockOutdated = value;
            }
        }

        int majorityWindow;
        public int MajorityWindow
        {
            get
            {
                return this.majorityWindow;
            }
            set
            {
                EnsureNotFrozen();
                this.majorityWindow = value;
            }
        }

        uint256 bIP34Hash;
        public uint256 BIP34Hash
        {
            get
            {
                return this.bIP34Hash;
            }
            set
            {
                EnsureNotFrozen();
                this.bIP34Hash = value;
            }
        }


        Target powLimit;
        public Target PowLimit
        {
            get
            {
                return this.powLimit;
            }
            set
            {
                EnsureNotFrozen();
                this.powLimit = value;
            }
        }


        TimeSpan powTargetTimespan;
        public TimeSpan PowTargetTimespan
        {
            get
            {
                return this.powTargetTimespan;
            }
            set
            {
                EnsureNotFrozen();
                this.powTargetTimespan = value;
            }
        }


        TimeSpan powTargetSpacing;
        public TimeSpan PowTargetSpacing
        {
            get
            {
                return this.powTargetSpacing;
            }
            set
            {
                EnsureNotFrozen();
                this.powTargetSpacing = value;
            }
        }


        bool powAllowMinDifficultyBlocks;
        public bool PowAllowMinDifficultyBlocks
        {
            get
            {
                return this.powAllowMinDifficultyBlocks;
            }
            set
            {
                EnsureNotFrozen();
                this.powAllowMinDifficultyBlocks = value;
            }
        }


        bool powNoRetargeting;
        public bool PowNoRetargeting
        {
            get
            {
                return this.powNoRetargeting;
            }
            set
            {
                EnsureNotFrozen();
                this.powNoRetargeting = value;
            }
        }


        uint256 hashGenesisBlock;
        public uint256 HashGenesisBlock
        {
            get
            {
                return this.hashGenesisBlock;
            }
            set
            {
                EnsureNotFrozen();
                this.hashGenesisBlock = value;
            }
        }

        uint256 minimumChainWork;
        public uint256 MinimumChainWork
        {
            get
            {
                return this.minimumChainWork;
            }
            set
            {
                EnsureNotFrozen();
                this.minimumChainWork = value;
            }
        }

        public long DifficultyAdjustmentInterval
        {
            get
            {
                return ((long)PowTargetTimespan.TotalSeconds / (long)PowTargetSpacing.TotalSeconds);
            }
        }

        int minerConfirmationWindow;
        public int MinerConfirmationWindow
        {
            get
            {
                return this.minerConfirmationWindow;
            }
            set
            {
                EnsureNotFrozen();
                this.minerConfirmationWindow = value;
            }
        }

        int ruleChangeActivationThreshold;
        public int RuleChangeActivationThreshold
        {
            get
            {
                return this.ruleChangeActivationThreshold;
            }
            set
            {
                EnsureNotFrozen();
                this.ruleChangeActivationThreshold = value;
            }
        }

        int coinType;

        /// <summary>
        /// Specify the BIP44 coin type for this network
        /// </summary>
        public int CoinType
        {
            get
            {
                return this.coinType;
            }
            set
            {
                EnsureNotFrozen();
                this.coinType = value;
            }
        }


        bool litecoinWorkCalculation;
        /// <summary>
        /// Specify using litecoin calculation for difficulty
        /// </summary>
        public bool LitecoinWorkCalculation
        {
            get
            {
                return this.litecoinWorkCalculation;
            }
            set
            {
                EnsureNotFrozen();
                this.litecoinWorkCalculation = value;
            }
        }

        BigInteger proofOfStakeLimit;
        public BigInteger ProofOfStakeLimit
        {
            get
            {
                return proofOfStakeLimit;
            }
            set
            {
                EnsureNotFrozen();
                proofOfStakeLimit = value;
            }
        }

        BigInteger proofOfStakeLimitV2;
        public BigInteger ProofOfStakeLimitV2
        {
            get
            {
                return proofOfStakeLimitV2;
            }
            set
            {
                EnsureNotFrozen();
                proofOfStakeLimitV2 = value;
            }
        }

        int lastPOWBlock;
        public int LastPOWBlock
        {
            get
            {
                return lastPOWBlock;
            }
            set
            {
                EnsureNotFrozen();
                lastPOWBlock = value;
            }
        }

        bool frozen = false;
        public void Freeze()
        {
            frozen = true;
        }
        private void EnsureNotFrozen()
        {
            if(frozen)
                throw new InvalidOperationException("This instance can't be modified");
        }

        public virtual Consensus Clone()
        {
            var consensus = new Consensus();
            Fill(consensus);
            return consensus;
        }

        protected void Fill(Consensus consensus)
        {
            consensus.EnsureNotFrozen();
            consensus.bIP34Hash = this.bIP34Hash;
            consensus.hashGenesisBlock = this.hashGenesisBlock;
            consensus.majorityEnforceBlockUpgrade = this.majorityEnforceBlockUpgrade;
            consensus.majorityRejectBlockOutdated = this.majorityRejectBlockOutdated;
            consensus.majorityWindow = this.majorityWindow;
            consensus.minerConfirmationWindow = this.minerConfirmationWindow;
            consensus.powAllowMinDifficultyBlocks = this.powAllowMinDifficultyBlocks;
            consensus.powLimit = this.powLimit;
            consensus.powNoRetargeting = this.powNoRetargeting;
            consensus.powTargetSpacing = this.powTargetSpacing;
            consensus.powTargetTimespan = this.powTargetTimespan;
            consensus.ruleChangeActivationThreshold = this.ruleChangeActivationThreshold;
            consensus.subsidyHalvingInterval = this.subsidyHalvingInterval;
            consensus.minimumChainWork = this.minimumChainWork;
            consensus.GetPoWHash = this.GetPoWHash;
            consensus.coinType = this.CoinType;
            consensus.litecoinWorkCalculation = this.litecoinWorkCalculation;
            consensus.LastPOWBlock = this.LastPOWBlock;
            consensus.ProofOfStakeLimit = this.ProofOfStakeLimit;
            consensus.ProofOfStakeLimitV2 = this.ProofOfStakeLimitV2;

        }
    }
    public partial class Network
    {
        private uint magic;
        private byte[] alertPubKeyArray;
        private PubKey alertPubKey;
        private List<DNSSeedData> seeds = new List<DNSSeedData>();
        private List<NetworkAddress> fixedSeeds = new List<NetworkAddress>();
        private Block genesis;
        private Consensus consensus = new Consensus();

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

        public long MinTxFee { get; private set; }

        public long FallbackFee { get; private set; }

        public long MinRelayTxFee { get; private set; }

        public int RPCPort { get; private set; }

        public int DefaultPort { get; private set; }

        public Consensus Consensus => this.consensus;

        public string Name { get; private set; }

        public IEnumerable<NetworkAddress> SeedNodes => this.fixedSeeds;

        public IEnumerable<DNSSeedData> DNSSeeds => this.seeds;

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
                        (byte)(this.Magic >> 8),
                        (byte)(this.Magic >> 16),
                        (byte)(this.Magic >> 24)
                    };
                    this.MagicBytesArray = bytes;
                }

                return this.MagicBytesArray;
            }
        }

        public uint Magic => this.magic;

        static readonly ConcurrentDictionary<string, Network> NetworksContainer = new ConcurrentDictionary<string, Network>();

        internal static Network Register(NetworkBuilder builder)
        {
            if (builder.Name == null)
                throw new InvalidOperationException("A network name need to be provided");

            if (GetNetwork(builder.Name) != null)
                throw new InvalidOperationException("The network " + builder.Name + " is already registered");

            Network network = new Network();
            network.Name = builder.Name;
            network.consensus = builder.Consensus;
            network.magic = builder.Magic;
            network.DefaultPort = builder.Port;
            network.RPCPort = builder.RPCPort;
            network.genesis = builder.Genesis;
            network.consensus.HashGenesisBlock = network.genesis.GetHash();
            network.consensus.Freeze();

            foreach (var seed in builder.Seeds)
            {
                network.seeds.Add(seed);
            }

            foreach (var seed in builder.FixedSeeds)
            {
                network.fixedSeeds.Add(seed);
            }

            network.base58Prefixes = Network.Main.base58Prefixes.ToArray();

            foreach (var kv in builder.Base58Prefixes)
            {
                network.base58Prefixes[(int) kv.Key] = kv.Value;
            }

            network.bech32Encoders = Network.Main.bech32Encoders.ToArray();

            foreach (var kv in builder.Bech32Prefixes)
            {
                network.bech32Encoders[(int) kv.Key] = kv.Value;
            }

            foreach (var alias in builder.Aliases)
            {
                NetworksContainer.TryAdd(alias.ToLowerInvariant(), network);
            }

            NetworksContainer.TryAdd(network.Name.ToLowerInvariant(), network);

            network.MinTxFee = builder.MinTxFee;
            network.FallbackFee = builder.FallbackFee;
            network.MinRelayTxFee = builder.MinRelayTxFee;

            return network;
        }

        private static void Assert(bool v)
        {
            if(!v)
                throw new InvalidOperationException("Invalid network");
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
            var type = GetBase58Type(base58);
            if(!type.HasValue)
                throw new FormatException("Invalid Base58 version");
            if(type == Base58Type.PUBKEY_ADDRESS)
                return new BitcoinPubKeyAddress(base58, this);
            if(type == Base58Type.SCRIPT_ADDRESS)
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
            for(int i = 0; i < base58Prefixes.Length; i++)
            {
                var prefix = base58Prefixes[i];
                if(prefix == null)
                    continue;
                if(bytes.Length < prefix.Length)
                    continue;
                if(Utils.ArrayEqual(bytes, 0, prefix, 0, prefix.Length))
                    return (Base58Type)i;
            }
            return null;
        }

        internal static Network GetNetworkFromBase58Data(string base58, Base58Type? expectedType = null)
        {
            foreach(var network in GetNetworks())
            {
                var type = network.GetBase58Type(base58);
                if(type.HasValue)
                {
                    if(expectedType != null && expectedType.Value != type.Value)
                        continue;
                    if(type.Value == Base58Type.COLORED_ADDRESS)
                    {
                        var raw = Encoders.Base58Check.DecodeData(base58);
                        var version = network.GetVersionBytes(type.Value, false);
                        if(version == null)
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
            if(str == null)
                throw new ArgumentNullException("str");

            var networks = expectedNetwork == null ? GetNetworks() : new[] { expectedNetwork };
            var maybeb58 = true;
            for (int i = 0; i < str.Length; i++)
            {
                if(!Base58Encoder.pszBase58Chars.Contains(str[i]))
                {
                    maybeb58 = false;
                    break;
                }
            }

            if(maybeb58)
            {
                try
                {
                    Encoders.Base58Check.DecodeData(str);
                }
                catch(FormatException) { maybeb58 = false; }
                if(maybeb58)
                {
                    foreach(var candidate in GetCandidates(networks, str))
                    {
                        bool rightNetwork = expectedNetwork == null || (candidate.Network == expectedNetwork);
                        bool rightType = candidate is T;
                        if(rightNetwork && rightType)
                            return (T)(object)candidate;
                    }
                    throw new FormatException("Invalid base58 string");
                }
            }

            foreach(var network in networks)
            {
                int i = -1;
                foreach(var encoder in network.bech32Encoders)
                {
                    i++;
                    if(encoder == null)
                        continue;
                    var type = (Bech32Type)i;
                    try
                    {
                        byte witVersion;
                        var bytes = encoder.Decode(str, out witVersion);
                        object candidate = null;

                        if(witVersion == 0 && bytes.Length == 20 && type == Bech32Type.WITNESS_PUBKEY_ADDRESS)
                            candidate = new BitcoinWitPubKeyAddress(str, network);
                        if(witVersion == 0 && bytes.Length == 32 && type == Bech32Type.WITNESS_SCRIPT_ADDRESS)
                            candidate = new BitcoinWitScriptAddress(str, network);

                        if(candidate is T)
                            return (T)(object)candidate;
                    }
                    catch(Bech32FormatException) { throw; }
                    catch(FormatException) { continue; }
                }

            }

            throw new FormatException("Invalid string");
        }

        private static IEnumerable<IBase58Data> GetCandidates(IEnumerable<Network> networks, string base58)
        {
            if(base58 == null)
                throw new ArgumentNullException("base58");
            foreach(var network in networks)
            {
                var type = network.GetBase58Type(base58);
                if(type.HasValue)
                {
                    if(type.Value == Base58Type.COLORED_ADDRESS)
                    {
                        var wrapped = BitcoinColoredAddress.GetWrappedBase58(base58, network);
                        var wrappedType = network.GetBase58Type(wrapped);
                        if(wrappedType == null)
                            continue;
                        try
                        {
                            var inner = network.CreateBase58Data(wrappedType.Value, wrapped);
                            if(inner.Network != network)
                                continue;
                        }
                        catch(FormatException) { }
                    }
                    IBase58Data data = null;
                    try
                    {
                        data = network.CreateBase58Data(type.Value, base58);
                    }
                    catch(FormatException) { }
                    if(data != null)
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
            var block = new Block();
            block.ReadWrite(this.genesis.ToBytes());
            return block;
        }

        public uint256 GenesisHash => this.consensus.HashGenesisBlock;

        public static IEnumerable<Network> GetNetworks()
        {
            yield return Main;
            yield return TestNet;
            yield return RegTest;

            if (NetworksContainer.Any())
            {
                List<Network>  others = NetworksContainer.Values.Distinct().ToList();

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
            if(name == null)
                throw new ArgumentNullException("name");

            if(NetworksContainer.Any())
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
            if(dest == null)
                throw new ArgumentNullException("dest");
            return new BitcoinPubKeyAddress(dest, this);
        }

        private BitcoinAddress CreateBitcoinScriptAddress(ScriptId scriptId)
        {
            return new BitcoinScriptAddress(scriptId, this);
        }

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


        public Money GetReward(int nHeight)
        {
            long nSubsidy = new Money(50 * Money.COIN);
            int halvings = nHeight / this.consensus.SubsidyHalvingInterval;

            // Force block reward to zero when right shift is undefined.
            if(halvings >= 64)
                return Money.Zero;

            // Subsidy is cut in half every 210,000 blocks which will occur approximately every 4 years.
            nSubsidy >>= halvings;

            return new Money(nSubsidy);
        }

        public bool ReadMagic(Stream stream, CancellationToken cancellation, bool throwIfEOF = false)
        {
            byte[] bytes = new byte[1];
            for(int i = 0; i < MagicBytes.Length; i++)
            {
                i = Math.Max(0, i);
                cancellation.ThrowIfCancellationRequested();

                var read = stream.ReadEx(bytes, 0, bytes.Length, cancellation);
                if(read == 0)
                    if(throwIfEOF)
                        throw new EndOfStreamException("No more bytes to read");
                    else
                        return false;
                if(read != 1)
                    i--;
                else if(this.MagicBytesArray[i] != bytes[0])
                    i = this.MagicBytesArray[0] == bytes[0] ? 0 : -1;
            }
            return true;
        }
    }
}