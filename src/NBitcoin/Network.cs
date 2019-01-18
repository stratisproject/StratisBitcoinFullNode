using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.DataEncoders;
using NBitcoin.Networks;
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

    /// <summary>
    /// A container of all network instances of a certain high level network.
    /// Every network normally comes in 3 flavors mainnet, testnet and regtest.
    /// </summary>
    public class NetworksSelector
    {
        public NetworksSelector(Func<Network> mainnet, Func<Network> testnet, Func<Network> regtest)
        {
            this.Mainnet = mainnet;
            this.Testnet = testnet;
            this.Regtest = regtest;
        }

        public Func<Network> Mainnet { get; }

        public Func<Network> Testnet { get; }

        public Func<Network> Regtest { get; }
    }

    public abstract class Network
    {
        protected Block Genesis;

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
        /// Be careful setting this: if you set it to zero then a transaction spammer can cheaply fill blocks using
        /// 1-satoshi-fee transactions. It should be set above the real cost to you of processing a transaction.
        /// </summary>
        /// <remarks>
        /// The <see cref="MinRelayTxFee"/> and <see cref="MinTxFee"/> are typically the same value to prevent dos attacks on the network. 
        /// If <see cref="MinRelayTxFee"/> is less than <see cref="MinTxFee"/>, an attacker can broadcast a lot of transactions with fees between these two values, 
        /// which will lead to transactions filling the mempool without ever being mined.
        /// </remarks>
        public long MinTxFee { get; protected set; }

        /// <summary>
        /// A fee rate that will be used when fee estimation has insufficient data.
        /// </summary>
        public long FallbackFee { get; protected set; }

        /// <summary>
        /// The minimum fee under which transactions may be rejected from being relayed.
        /// </summary>
        /// <remarks>
        /// The <see cref="MinRelayTxFee"/> and <see cref="MinTxFee"/> are typically the same value to prevent dos attacks on the network. 
        /// If <see cref="MinRelayTxFee"/> is less than <see cref="MinTxFee"/>, an attacker can broadcast a lot of transactions with fees between these two values, 
        /// which will lead to transactions filling the mempool without ever being mined.
        /// </remarks>
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
        /// The default maximum number of outbound connections a node on this network will form.
        /// </summary>
        public int DefaultMaxOutboundConnections { get; protected set; }

        /// <summary>
        /// The default maximum number of inbound connections a node on this network will accept.
        /// </summary>
        public int DefaultMaxInboundConnections { get; protected set; }

        /// <summary>
        /// The consensus for this network.
        /// </summary>
        public IConsensus Consensus { get; protected set; }

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
        /// An indicative coin ticker for use with external applications.
        /// </summary>
        public string CoinTicker { get; set; }

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
        /// The list of script templates regarded as standard.
        /// Standardness is a distinct property from consensus validity.
        /// A non-standard transaction can still be mined/staked by a willing node and the resulting block will be accepted by the network.
        /// However, a non-standard transaction will typically not be relayed between nodes.
        /// </summary>
        public IStandardScriptsRegistry StandardScriptsRegistry { get; protected set; }

        /// <summary>
        /// Mines a new genesis block, to use with a new network.
        /// Typically, 3 such genesis blocks need to be created when bootstrapping a new coin: for Main, Test and Reg networks.
        /// </summary>
        /// <param name="consensusFactory">
        /// The consensus factory used to create transactions and blocks. 
        /// Use <see cref="PosConsensusFactory"/> for proof-of-stake based networks.
        /// </param>
        /// <param name="coinbaseText">
        /// Traditionally a news headline from the day of the launch, but could be any string or link.
        /// This will be inserted in the input coinbase transaction script.
        /// It should be shorter than 92 characters.
        /// </param>
        /// <param name="target">
        /// The difficulty target under which the hash of the block need to be. 
        /// Some more details: As an example, the target for the Stratis Main network is 00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff.
        /// To make it harder to mine the genesis block, have more zeros at the beginning (keeping the length the same). This will make the target smaller, so finding a number under it will be more difficult.
        /// To make it easier to mine the genesis block ,do the opposite. Example of an easy one: 00ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff.
        /// Make the Test and Reg targets ones easier to find than the Main one so that you don't wait too long to mine the genesis block.
        /// </param>
        /// <param name="genesisReward">
        /// Specify how many coins to put in the genesis transaction's output. These coins are unspendable.
        /// </param>
        /// <param name="version">
        /// The version of the transaction and the block header set in the genesis block. 
        /// </param>
        /// <example>
        /// The following example shows the creation of a genesis block.
        /// <code>
        /// Block genesis = MineGenesisBlock(new PosConsensusFactory(), "Some topical headline.", new Target(new uint256("000fffff00000000000000000000000000000000000000000000000000000000")), Money.Coins(50m));
        /// BlockHeader header = genesis.Header;
        /// Console.WriteLine("Make a note of the following values:");
        /// Console.WriteLine("bits: " + header.Bits);
        /// Console.WriteLine("nonce: " + header.Nonce);
        /// Console.WriteLine("time: " + header.Time);
        /// Console.WriteLine("version: " + header.Version);
        /// Console.WriteLine("hash: " + header.GetHash());
        /// Console.WriteLine("merkleroot: " + header.HashMerkleRoot);
        /// </code>
        /// </example>
        /// <returns>A genesis block.</returns>
        public static Block MineGenesisBlock(ConsensusFactory consensusFactory, string coinbaseText, Target target, Money genesisReward, int version = 1)
        {
            if (consensusFactory == null)
                throw new ArgumentException($"Parameter '{nameof(consensusFactory)}' cannot be null. Use 'new ConsensusFactory()' for Bitcoin-like proof-of-work blockchains and 'new PosConsensusFactory()' for Stratis-like proof-of-stake blockchains.");

            if (string.IsNullOrEmpty(coinbaseText))
                throw new ArgumentException($"Parameter '{nameof(coinbaseText)}' cannot be null. Use a news headline or any other appropriate string.");

            if (target == null)
                throw new ArgumentException($"Parameter '{nameof(target)}' cannot be null. Example use: new Target(new uint256(\"0000ffff00000000000000000000000000000000000000000000000000000000\"))");

            if (coinbaseText.Length >= 92)
                throw new ArgumentException($"Parameter '{nameof(coinbaseText)}' should be shorter than 92 characters.");

            if (genesisReward == null)
                throw new ArgumentException($"Parameter '{nameof(genesisReward)}' cannot be null. Example use: 'Money.Coins(50m)'.");

            DateTimeOffset time = DateTimeOffset.Now;
            uint unixTime = Utils.DateTimeToUnixTime(time);

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = (uint)version;
            txNew.Time = unixTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(
                    Op.GetPushOp(0),
                    new Op()
                    {
                        Code = (OpcodeType)0x1,
                        PushData = new[] { (byte)42 }
                    },
                    Op.GetPushOp(Encoders.ASCII.DecodeData(coinbaseText)))
            });

            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = time;
            genesis.Header.Bits = target;
            genesis.Header.Nonce = 0;
            genesis.Header.Version = version;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            // Iterate over the nonce until the proof-of-work is valid.
            // This will mean the block header hash is under the target.
            while (!genesis.CheckProofOfWork())
            {
                genesis.Header.Nonce++;
                if (genesis.Header.Nonce == 0)
                    genesis.Header.Time++;
            }

            return genesis;
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
                return CreateBitcoinPubKeyAddress(base58);
            if (type == Base58Type.SCRIPT_ADDRESS)
                return CreateBitcoinScriptAddress(base58);
            throw new FormatException("Invalid Base58 version");
        }

        public Base58Type? GetBase58Type(string base58)
        {
            byte[] bytes = Encoders.Base58Check.DecodeData(base58);
            for (int i = 0; i < this.Base58Prefixes.Length; i++)
            {
                byte[] prefix = this.Base58Prefixes[i];
                if (prefix == null)
                    continue;
                if (bytes.Length < prefix.Length)
                    continue;
                if (Utils.ArrayEqual(bytes, 0, prefix, 0, prefix.Length))
                    return (Base58Type)i;
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

            IEnumerable<Network> networks = expectedNetwork == null ? NetworkRegistration.GetNetworks() : new[] { expectedNetwork };
            bool maybeb58 = true;
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
                            return (T)(object)candidate;
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
                    var type = (Bech32Type)i;
                    try
                    {
                        byte witVersion;
                        byte[] bytes = encoder.Decode(str, out witVersion);
                        object candidate = null;

                        if (witVersion == 0 && bytes.Length == 20 && type == Bech32Type.WITNESS_PUBKEY_ADDRESS)
                            candidate = new BitcoinWitPubKeyAddress(str, network);
                        if (witVersion == 0 && bytes.Length == 32 && type == Bech32Type.WITNESS_SCRIPT_ADDRESS)
                            candidate = new BitcoinWitScriptAddress(str, network);

                        if (candidate is T)
                            return (T)candidate;
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
                        string wrapped = BitcoinColoredAddress.GetWrappedBase58(base58, network);
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
                return CreateBitcoinExtPubKey(base58);
            if (type == Base58Type.EXT_SECRET_KEY)
                return CreateBitcoinExtKey(base58);
            if (type == Base58Type.PUBKEY_ADDRESS)
                return CreateBitcoinPubKeyAddress(base58);
            if (type == Base58Type.SCRIPT_ADDRESS)
                return CreateBitcoinScriptAddress(base58);
            if (type == Base58Type.SECRET_KEY)
                return CreateBitcoinSecret(base58);
            if (type == Base58Type.CONFIRMATION_CODE)
                return CreateConfirmationCode(base58);
            if (type == Base58Type.ENCRYPTED_SECRET_KEY_EC)
                return CreateEncryptedKeyEC(base58);
            if (type == Base58Type.ENCRYPTED_SECRET_KEY_NO_EC)
                return CreateEncryptedKeyNoEC(base58);
            if (type == Base58Type.PASSPHRASE_CODE)
                return CreatePassphraseCode(base58);
            if (type == Base58Type.STEALTH_ADDRESS)
                return CreateStealthAddress(base58);
            if (type == Base58Type.ASSET_ID)
                return CreateAssetId(base58);
            if (type == Base58Type.COLORED_ADDRESS)
                return CreateColoredAddress(base58);
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

        public OpenAsset.BitcoinAssetId CreateAssetId(string base58)
        {
            return new OpenAsset.BitcoinAssetId(base58, this);
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
            return Block.Load(this.Genesis.ToBytes(this.Consensus.ConsensusFactory), this);
        }

        public uint256 GenesisHash => this.Consensus.HashGenesisBlock;

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
            var bytes = new byte[1];
            for (int i = 0; i < this.MagicBytes.Length; i++)
            {
                i = Math.Max(0, i);
                cancellation.ThrowIfCancellationRequested();

                int read = stream.ReadEx(bytes, 0, bytes.Length, cancellation);
                if (read == 0)
                {
                    if (throwIfEOF)
                        throw new EndOfStreamException("No more bytes to read");
                    else
                        return false;
                }

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
            {
                throw new NotImplementedException("The network " + this + " does not have any prefix for bech32 " +
                                                  Enum.GetName(typeof(Bech32Type), type));
            }

            return encoder;
        }

        public byte[] GetVersionBytes(Base58Type type, bool throws)
        {
            byte[] prefix = this.Base58Prefixes[(int)type];
            if (prefix == null && throws)
            {
                throw new NotImplementedException("The network " + this + " does not have any prefix for base58 " +
                                                  Enum.GetName(typeof(Base58Type), type));
            }

            return prefix?.ToArray();
        }

        internal static string CreateBase58(Base58Type type, byte[] bytes, Network network)
        {
            if (network == null)
                throw new ArgumentNullException("network");
            if (bytes == null)
                throw new ArgumentNullException("bytes");
            byte[] versionBytes = network.GetVersionBytes(type, true);
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
            var rand = new Random();
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

        public Block CreateBlock()
        {
            return this.Consensus.ConsensusFactory.CreateBlock();
        }

        public Transaction CreateTransaction()
        {
            return this.Consensus.ConsensusFactory.CreateTransaction();
        }

        public Transaction CreateTransaction(string hex)
        {
            return this.Consensus.ConsensusFactory.CreateTransaction(hex);
        }

        public Transaction CreateTransaction(byte[] bytes)
        {
            return this.Consensus.ConsensusFactory.CreateTransaction(bytes);
        }
    }
}