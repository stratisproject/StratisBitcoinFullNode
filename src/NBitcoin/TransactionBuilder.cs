using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin.BuilderExtensions;
using NBitcoin.Crypto;
using NBitcoin.OpenAsset;
using NBitcoin.Policy;
using NBitcoin.Stealth;
using Builder = System.Func<NBitcoin.TransactionBuilder.TransactionBuildingContext, NBitcoin.IMoney>;

namespace NBitcoin
{
    [Flags]
    public enum ChangeType : int
    {
        All = 3,
        Colored = 1,
        Uncolored = 2
    }
    public interface ICoinSelector
    {
        IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target);
    }

    /// <summary>
    /// A coin selector that selects all the coins passed by default.
    /// Useful when a user wants a specific set of coins to be spent.
    /// </summary>
    public class AllCoinsSelector : ICoinSelector
    {
        public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
        {
            return coins;
        }
    }

    /// <summary>
    /// Algorithm implemented by bitcoin core https://github.com/bitcoin/bitcoin/blob/master/src/wallet.cpp#L1276
    /// Minimize the change
    /// </summary>
    public class DefaultCoinSelector : ICoinSelector
    {
        public DefaultCoinSelector()
        {

        }

        private Random _Rand = new Random();
        public DefaultCoinSelector(int seed)
        {
            this._Rand = new Random(seed);
        }

        /// <summary>
        /// Select all coins belonging to same scriptPubKey together to protect privacy. (Default: true)
        /// </summary>
        public bool GroupByScriptPubKey
        {
            get; set;
        } = true;

        #region ICoinSelector Members

        public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
        {
            IMoney zero = target.Sub(target);

            var result = new List<ICoin>();
            IMoney total = zero;

            if (target.CompareTo(zero) == 0)
                return result;

            var orderedCoinGroups = coins.GroupBy(c => this.GroupByScriptPubKey ? c.TxOut.ScriptPubKey : new Key().ScriptPubKey)
                                    .Select(scriptPubKeyCoins => new
                                    {
                                        Amount = scriptPubKeyCoins.Select(c => c.Amount).Sum(zero),
                                        Coins = scriptPubKeyCoins.ToList()
                                    }).OrderBy(c => c.Amount);


            var targetCoin = orderedCoinGroups
                            .FirstOrDefault(c => c.Amount.CompareTo(target) == 0);
            //If any of your UTXO² matches the Target¹ it will be used.
            if (targetCoin != null)
                return targetCoin.Coins;

            foreach (var coinGroup in orderedCoinGroups)
            {
                if (coinGroup.Amount.CompareTo(target) == -1 && total.CompareTo(target) == -1)
                {
                    total = total.Add(coinGroup.Amount);
                    result.AddRange(coinGroup.Coins);
                    //If the "sum of all your UTXO smaller than the Target" happens to match the Target, they will be used. (This is the case if you sweep a complete wallet.)
                    if (total.CompareTo(target) == 0)
                        return result;

                }
                else
                {
                    if (total.CompareTo(target) == -1 && coinGroup.Amount.CompareTo(target) == 1)
                    {
                        //If the "sum of all your UTXO smaller than the Target" doesn't surpass the target, the smallest UTXO greater than your Target will be used.
                        return coinGroup.Coins;
                    }
                    else
                    {
                        //						Else Bitcoin Core does 1000 rounds of randomly combining unspent transaction outputs until their sum is greater than or equal to the Target. If it happens to find an exact match, it stops early and uses that.
                        //Otherwise it finally settles for the minimum of
                        //the smallest UTXO greater than the Target
                        //the smallest combination of UTXO it discovered in Step 4.
                        var allCoins = orderedCoinGroups.ToArray();
                        IMoney minTotal = null;
                        for (int _ = 0; _ < 1000; _++)
                        {
                            var selection = new List<ICoin>();
                            Utils.Shuffle(allCoins, this._Rand);
                            total = zero;
                            for (int i = 0; i < allCoins.Length; i++)
                            {
                                selection.AddRange(allCoins[i].Coins);
                                total = total.Add(allCoins[i].Amount);
                                if (total.CompareTo(target) == 0)
                                    return selection;
                                if (total.CompareTo(target) == 1)
                                    break;
                            }
                            if (total.CompareTo(target) == -1)
                            {
                                return null;
                            }
                            if (minTotal == null || total.CompareTo(minTotal) == -1)
                            {
                                minTotal = total;
                            }
                        }
                    }
                }
            }
            if (total.CompareTo(target) == -1)
                return null;
            return result;
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when not enough funds are present for verifying or building a transaction
    /// </summary>
    public class NotEnoughFundsException : Exception
    {
        public NotEnoughFundsException(string message, string group, IMoney missing)
            : base(BuildMessage(message, group, missing))
        {
            this.Missing = missing;
            this.Group = group;
        }

        private static string BuildMessage(string message, string group, IMoney missing)
        {
            var builder = new StringBuilder();
            builder.Append(message);
            if(group != null)
                builder.Append(" in group " + group);
            if(missing != null)
                builder.Append(" with missing amount " + missing);
            return builder.ToString();
        }
        public NotEnoughFundsException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// The group name who is missing the funds
        /// </summary>
        public string Group
        {
            get;
            set;
        }

        /// <summary>
        /// Amount of Money missing
        /// </summary>
        public IMoney Missing
        {
            get;
            set;
        }
    }

    /// <summary>
    /// A class for building and signing all sort of transactions easily (http://www.codeproject.com/Articles/835098/NBitcoin-Build-Them-All)
    /// </summary>
    public class TransactionBuilder
    {
        internal class TransactionBuilderSigner : ISigner
        {
            private ICoin coin;
            private SigHash sigHash;
            private IndexedTxIn txIn;
            private TransactionBuilder builder;
            public TransactionBuilderSigner(TransactionBuilder builder, ICoin coin, SigHash sigHash, IndexedTxIn txIn)
            {
                this.builder = builder;
                this.coin = coin;
                this.sigHash = sigHash;
                this.txIn = txIn;
            }
            #region ISigner Members

            public TransactionSignature Sign(Key key)
            {
                return this.txIn.Sign(this.builder.Network, key, this.coin, this.sigHash);
            }

            #endregion
        }
        internal class TransactionBuilderKeyRepository : IKeyRepository
        {
            private TransactionSigningContext _Ctx;
            private TransactionBuilder _TxBuilder;
            public TransactionBuilderKeyRepository(TransactionBuilder txBuilder, TransactionSigningContext ctx)
            {
                this._Ctx = ctx;
                this._TxBuilder = txBuilder;
            }
            #region IKeyRepository Members

            public Key FindKey(Script scriptPubkey)
            {
                return this._TxBuilder.FindKey(this._Ctx, scriptPubkey);
            }

            #endregion
        }

        private class KnownSignatureSigner : ISigner, IKeyRepository
        {
            private ICoin coin;
            private SigHash sigHash;
            private IndexedTxIn txIn;
            private List<Tuple<PubKey, ECDSASignature>> _KnownSignatures;
            private Dictionary<KeyId, ECDSASignature> _VerifiedSignatures = new Dictionary<KeyId, ECDSASignature>();
            private Dictionary<uint256, PubKey> _DummyToRealKey = new Dictionary<uint256, PubKey>();
            private TransactionBuilder builder;

            public KnownSignatureSigner(TransactionBuilder builder, List<Tuple<PubKey, ECDSASignature>> _KnownSignatures, ICoin coin, SigHash sigHash, IndexedTxIn txIn)
            {
                this.builder = builder;
                this._KnownSignatures = _KnownSignatures;
                this.coin = coin;
                this.sigHash = sigHash;
                this.txIn = txIn;
            }

            public Key FindKey(Script scriptPubKey)
            {
                foreach(Tuple<PubKey, ECDSASignature> tv in this._KnownSignatures.Where(tv => IsCompatibleKey(tv.Item1, scriptPubKey)))
                {
                    uint256 hash = this.txIn.GetSignatureHash(this.builder.Network, this.coin, this.sigHash);
                    if(tv.Item1.Verify(hash, tv.Item2))
                    {
                        var key = new Key();
                        this._DummyToRealKey.Add(Hashes.Hash256(key.PubKey.ToBytes()), tv.Item1);
                        this._VerifiedSignatures.AddOrReplace(key.PubKey.Hash, tv.Item2);
                        return key;
                    }
                }
                return null;
            }

            public Script ReplaceDummyKeys(Script script)
            {
                List<Op> ops = script.ToOps().ToList();
                var result = new List<Op>();
                foreach(Op op in ops)
                {
                    uint256 h = Hashes.Hash256(op.PushData);
                    PubKey real;
                    if(this._DummyToRealKey.TryGetValue(h, out real))
                        result.Add(Op.GetPushOp(real.ToBytes()));
                    else
                        result.Add(op);
                }
                return new Script(result.ToArray());
            }

            public TransactionSignature Sign(Key key)
            {
                return new TransactionSignature(this._VerifiedSignatures[key.PubKey.Hash], this.sigHash);
            }
        }

        internal class TransactionSigningContext
        {
            public TransactionSigningContext(TransactionBuilder builder, Transaction transaction)
            {
                this.Builder = builder;
                this.Transaction = transaction;
            }

            public Transaction Transaction
            {
                get;
                set;
            }
            public TransactionBuilder Builder
            {
                get;
                set;
            }

            private readonly List<Key> _AdditionalKeys = new List<Key>();
            public List<Key> AdditionalKeys
            {
                get
                {
                    return this._AdditionalKeys;
                }
            }

            public SigHash SigHash
            {
                get;
                set;
            }
        }

        internal class TransactionBuildingContext
        {
            public TransactionBuildingContext(TransactionBuilder builder)
            {
                this.Builder = builder;
                this.Transaction = builder.Network.CreateTransaction();
                this.AdditionalFees = Money.Zero;
            }

            public BuilderGroup Group
            {
                get;
                set;
            }

            private readonly List<ICoin> _ConsumedCoins = new List<ICoin>();
            public List<ICoin> ConsumedCoins
            {
                get
                {
                    return this._ConsumedCoins;
                }
            }

            public TransactionBuilder Builder
            {
                get;
                set;
            }

            public Transaction Transaction
            {
                get;
                set;
            }

            public Money AdditionalFees
            {
                get;
                set;
            }

            private readonly List<Builder> _AdditionalBuilders = new List<Builder>();
            public List<Builder> AdditionalBuilders
            {
                get
                {
                    return this._AdditionalBuilders;
                }
            }

            private ColorMarker _Marker;

            public ColorMarker GetColorMarker(bool issuance)
            {
                if(this._Marker == null) this._Marker = new ColorMarker();
                if(!issuance)
                    EnsureMarkerInserted();
                return this._Marker;
            }

            private TxOut EnsureMarkerInserted()
            {
                uint position;
                TxIn dummy = this.Transaction.AddInput(new TxIn(new OutPoint(new uint256(1), 0))); //Since a transaction without input will be considered without marker, insert a dummy
                try
                {
                    if(ColorMarker.Get(this.Transaction, out position) != null)
                        return this.Transaction.Outputs[position];
                }
                finally
                {
                    this.Transaction.Inputs.Remove(dummy);
                }
                TxOut txout = this.Transaction.AddOutput(new TxOut()
                {
                    ScriptPubKey = new ColorMarker().GetScript()
                });
                txout.Value = Money.Zero;
                return txout;
            }

            public void Finish()
            {
                if(this._Marker != null)
                {
                    TxOut txout = EnsureMarkerInserted();
                    txout.ScriptPubKey = this._Marker.GetScript();
                }
            }

            public IssuanceCoin IssuanceCoin
            {
                get;
                set;
            }

            public IMoney ChangeAmount
            {
                get;
                set;
            }

            public TransactionBuildingContext CreateMemento()
            {
                var memento = new TransactionBuildingContext(this.Builder);
                memento.RestoreMemento(this);
                return memento;
            }

            public void RestoreMemento(TransactionBuildingContext memento)
            {
                this._Marker = memento._Marker == null ? null : new ColorMarker(memento._Marker.GetScript());
                this.Transaction = memento.Builder.Network.CreateTransaction(memento.Transaction.ToBytes());
                this.AdditionalFees = memento.AdditionalFees;
            }

            public bool NonFinalSequenceSet
            {
                get;
                set;
            }

            public IMoney CoverOnly
            {
                get;
                set;
            }

            public IMoney Dust
            {
                get;
                set;
            }

            public ChangeType ChangeType
            {
                get;
                set;
            }
        }

        internal class BuilderGroup
        {
            private TransactionBuilder _Parent;
            public BuilderGroup(TransactionBuilder parent)
            {
                this._Parent = parent;
                this.FeeWeight = 1.0m;
                this.Builders.Add(SetChange);
            }

            private IMoney SetChange(TransactionBuildingContext ctx)
            {
                var changeAmount = (Money)ctx.ChangeAmount;
                if(changeAmount.Satoshi == 0)
                    return Money.Zero;
                ctx.Transaction.AddOutput(new TxOut(changeAmount, ctx.Group.ChangeScript[(int)ChangeType.Uncolored]));
                return changeAmount;
            }
            internal List<Builder> Builders = new List<Builder>();
            internal Dictionary<OutPoint, ICoin> Coins = new Dictionary<OutPoint, ICoin>();
            internal List<Builder> IssuanceBuilders = new List<Builder>();
            internal Dictionary<AssetId, List<Builder>> BuildersByAsset = new Dictionary<AssetId, List<Builder>>();
            internal Script[] ChangeScript = new Script[3];
            internal void Shuffle()
            {
                Shuffle(this.Builders);
                foreach(KeyValuePair<AssetId, List<Builder>> builders in this.BuildersByAsset)
                    Shuffle(builders.Value);
                Shuffle(this.IssuanceBuilders);
            }
            private void Shuffle(List<Builder> builders)
            {
                Utils.Shuffle(builders, this._Parent._Rand);
            }

            public Money CoverOnly
            {
                get;
                set;
            }

            public string Name
            {
                get;
                set;
            }

            public decimal FeeWeight
            {
                get;
                set;
            }
        }

        private List<BuilderGroup> _BuilderGroups = new List<BuilderGroup>();
        private BuilderGroup _CurrentGroup = null;
        internal BuilderGroup CurrentGroup
        {
            get
            {
                if(this._CurrentGroup == null)
                {
                    this._CurrentGroup = new BuilderGroup(this);
                    this._BuilderGroups.Add(this._CurrentGroup);
                }
                return this._CurrentGroup;
            }
        }

        public TransactionBuilder(Network network)
        {
            this.Network = network;

            this._Rand = new Random();
            this.CoinSelector = new DefaultCoinSelector();
            this.StandardTransactionPolicy = new StandardTransactionPolicy(this.Network);
            this.DustPrevention = true;
            InitExtensions();
        }

        private void InitExtensions()
        {
            this.Extensions.Add(new P2PKHBuilderExtension());
            this.Extensions.Add(new P2MultiSigBuilderExtension());
            this.Extensions.Add(new P2PKBuilderExtension());
            this.Extensions.Add(new OPTrueExtension());
        }

        internal Random _Rand;

        public TransactionBuilder(int seed, Network network)
        {
            this.Network = network;

            this._Rand = new Random(seed);
            this.CoinSelector = new DefaultCoinSelector(seed);
            this.StandardTransactionPolicy = new StandardTransactionPolicy(this.Network);
            this.DustPrevention = true;
            InitExtensions();
        }

        public ICoinSelector CoinSelector
        {
            get;
            set;
        }

        /// <summary>
        /// This field should be mandatory in the constructor.
        /// </summary>
        public Network Network
        {
            get;
            private set;
        }

        /// <summary>
        /// Will transform transfers below Dust, so the transaction get correctly relayed by the network.
        /// If true, it will remove any TxOut below Dust, so the transaction get correctly relayed by the network. (Default: true)
        /// </summary>
        public bool DustPrevention
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the TransactionBuilder will not select coins whose fee to spend is higher than its value. (Default: true)
        /// The cost of spending a coin is based on the <see cref="FilterUneconomicalCoinsRate"/>.
        /// </summary>
        public bool FilterUneconomicalCoins { get; set; } = true;

        /// <summary>
        /// If <see cref="FilterUneconomicalCoins"/> is true, this rate is used to know if an output is economical.
        /// This property is set automatically when calling <see cref="SendEstimatedFees(FeeRate)"/> or <see cref="SendEstimatedFeesSplit(FeeRate)"/>.
        /// </summary>
        public FeeRate FilterUneconomicalCoinsRate
        {
            get; set;
        }

        /// <summary>
        /// A callback used by the TransactionBuilder when it does not find the coin for an input
        /// </summary>
        public Func<OutPoint, ICoin> CoinFinder
        {
            get;
            set;
        }

        /// <summary>
        /// A callback used by the TransactionBuilder when it does not find the key for a scriptPubKey
        /// </summary>
        public Func<Script, Key> KeyFinder
        {
            get;
            set;
        }

        private LockTime? _LockTime;
        public TransactionBuilder SetLockTime(LockTime lockTime)
        {
            this._LockTime = lockTime;
            return this;
        }

        private uint? _TimeStamp;
        public TransactionBuilder SetTimeStamp(uint timeStamp)
        {
            this._TimeStamp = timeStamp;
            return this;
        }

        private List<Key> _Keys = new List<Key>();

        public TransactionBuilder AddKeys(params ISecret[] keys)
        {
            AddKeys(keys.Select(k => k.PrivateKey).ToArray());
            return this;
        }

        public TransactionBuilder AddKeys(params Key[] keys)
        {
            this._Keys.AddRange(keys);
            foreach (Key k in keys)
            {
                AddKnownRedeems(k.PubKey.ScriptPubKey);
                AddKnownRedeems(k.PubKey.WitHash.ScriptPubKey);
                AddKnownRedeems(k.PubKey.Hash.ScriptPubKey);
            }
            return this;
        }

        public TransactionBuilder AddKnownSignature(PubKey pubKey, TransactionSignature signature)
        {
            if(pubKey == null)
                throw new ArgumentNullException("pubKey");
            if(signature == null)
                throw new ArgumentNullException("signature");
            this._KnownSignatures.Add(Tuple.Create(pubKey, signature.Signature));
            return this;
        }

        public TransactionBuilder AddKnownSignature(PubKey pubKey, ECDSASignature signature)
        {
            if(pubKey == null)
                throw new ArgumentNullException("pubKey");
            if(signature == null)
                throw new ArgumentNullException("signature");
            this._KnownSignatures.Add(Tuple.Create(pubKey, signature));
            return this;
        }

        public TransactionBuilder AddCoins(params ICoin[] coins)
        {
            return AddCoins((IEnumerable<ICoin>)coins);
        }

        public TransactionBuilder AddCoins(IEnumerable<ICoin> coins)
        {
            foreach(ICoin coin in coins)
            {
                this.CurrentGroup.Coins.AddOrReplace(coin.Outpoint, coin);
            }
            return this;
        }

        /// <summary>
        /// Set the name of this group (group are separated by call to Then())
        /// </summary>
        /// <param name="groupName">Name of the group</param>
        /// <returns></returns>
        public TransactionBuilder SetGroupName(string groupName)
        {
            this.CurrentGroup.Name = groupName;
            return this;
        }

        /// <summary>
        /// Send bitcoins to a destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="amount">The amount</param>
        /// <returns></returns>
        public TransactionBuilder Send(IDestination destination, Money amount)
        {
            return Send(destination.ScriptPubKey, amount);
        }

        private readonly static TxNullDataTemplate _OpReturnTemplate = new TxNullDataTemplate(1024 * 1024);

        /// <summary>
        /// Send bitcoins to a destination
        /// </summary>
        /// <param name="scriptPubKey">The destination</param>
        /// <param name="amount">The amount</param>
        /// <returns></returns>
        public TransactionBuilder Send(Script scriptPubKey, Money amount)
        {
            if(amount < Money.Zero)
                throw new ArgumentOutOfRangeException("amount", "amount can't be negative");
            this._LastSendBuilder = null; //If the amount is dust, we don't want the fee to be paid by the previous Send
            if(this.DustPrevention && amount < GetDust(scriptPubKey) && !_OpReturnTemplate.CheckScriptPubKey(scriptPubKey))
            {
                SendFees(amount);
                return this;
            }

            var builder = new SendBuilder(new TxOut(amount, scriptPubKey));
            this.CurrentGroup.Builders.Add(builder.Build);
            this._LastSendBuilder = builder;
            return this;
        }

        private SendBuilder _LastSendBuilder;
        private SendBuilder _SubstractFeeBuilder;

        private class SendBuilder
        {
            internal TxOut _TxOut;

            public SendBuilder(TxOut txout)
            {
                this._TxOut = txout;
            }

            public Money Build(TransactionBuildingContext ctx)
            {
                ctx.Transaction.Outputs.Add(this._TxOut);
                return this._TxOut.Value;
            }
        }

        /// <summary>
        /// Will subtract fees from the previous TxOut added by the last TransactionBuidler.Send() call
        /// </summary>
        /// <returns></returns>
        public TransactionBuilder SubtractFees()
        {
            if(this._LastSendBuilder == null)
                throw new InvalidOperationException("No call to TransactionBuilder.Send has been done which can support the fees");
            this._SubstractFeeBuilder = this._LastSendBuilder;
            return this;
        }

        /// <summary>
        /// Send a money amount to the destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="amount">The amount (supported : Money, AssetMoney, MoneyBag)</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">The coin type is not supported</exception>
        public TransactionBuilder Send(IDestination destination, IMoney amount)
        {
            return Send(destination.ScriptPubKey, amount);
        }
        /// <summary>
        /// Send a money amount to the destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="amount">The amount (supported : Money, AssetMoney, MoneyBag)</param>
        /// <returns></returns>
        /// <exception cref="System.NotSupportedException">The coin type is not supported</exception>
        public TransactionBuilder Send(Script scriptPubKey, IMoney amount)
        {
            var bag = amount as MoneyBag;
            if(bag != null)
            {
                foreach(IMoney money in bag)
                    Send(scriptPubKey, amount);
                return this;
            }
            var coinAmount = amount as Money;
            if(coinAmount != null)
                return Send(scriptPubKey, coinAmount);
            var assetAmount = amount as AssetMoney;
            if(assetAmount != null)
                return SendAsset(scriptPubKey, assetAmount);
            throw new NotSupportedException("Type of Money not supported");
        }

        /// <summary>
        /// Send assets (Open Asset) to a destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="asset">The asset and amount</param>
        /// <returns></returns>
        public TransactionBuilder SendAsset(IDestination destination, AssetMoney asset)
        {
            return SendAsset(destination.ScriptPubKey, asset);
        }

        /// <summary>
        /// Send assets (Open Asset) to a destination
        /// </summary>
        /// <param name="destination">The destination</param>
        /// <param name="asset">The asset and amount</param>
        /// <returns></returns>
        public TransactionBuilder SendAsset(IDestination destination, AssetId assetId, ulong quantity)
        {
            return SendAsset(destination, new AssetMoney(assetId, quantity));
        }

        public TransactionBuilder Shuffle()
        {
            Utils.Shuffle(this._BuilderGroups, this._Rand);
            foreach(BuilderGroup group in this._BuilderGroups)
                group.Shuffle();
            return this;
        }

        private IMoney SetColoredChange(TransactionBuildingContext ctx)
        {
            var changeAmount = (AssetMoney)ctx.ChangeAmount;
            if(changeAmount.Quantity == 0)
                return changeAmount;
            ColorMarker marker = ctx.GetColorMarker(false);
            Script script = ctx.Group.ChangeScript[(int)ChangeType.Colored];
            TxOut txout = ctx.Transaction.AddOutput(new TxOut(GetDust(script), script));
            marker.SetQuantity(ctx.Transaction.Outputs.Count - 2, changeAmount.Quantity);
            ctx.AdditionalFees += txout.Value;
            return changeAmount;
        }

        public TransactionBuilder SendAsset(Script scriptPubKey, AssetId assetId, ulong assetQuantity)
        {
            return SendAsset(scriptPubKey, new AssetMoney(assetId, assetQuantity));
        }

        public TransactionBuilder SendAsset(Script scriptPubKey, AssetMoney asset)
        {
            if(asset.Quantity < 0)
                throw new ArgumentOutOfRangeException("asset", "Asset amount can't be negative");
            if(asset.Quantity == 0)
                return this;
            AssertOpReturn("Colored Coin");
            List<Builder> builders = this.CurrentGroup.BuildersByAsset.TryGet(asset.Id);
            if(builders == null)
            {
                builders = new List<Builder>();
                this.CurrentGroup.BuildersByAsset.Add(asset.Id, builders);
                builders.Add(SetColoredChange);
            }
            builders.Add(ctx =>
            {
                ColorMarker marker = ctx.GetColorMarker(false);
                TxOut txout = ctx.Transaction.AddOutput(new TxOut(GetDust(scriptPubKey), scriptPubKey));
                marker.SetQuantity(ctx.Transaction.Outputs.Count - 2, asset.Quantity);
                ctx.AdditionalFees += txout.Value;
                return asset;
            });
            return this;
        }

        private Money GetDust()
        {
            return GetDust(new Script(new byte[25]));
        }

        private Money GetDust(Script script)
        {
            if(this.StandardTransactionPolicy == null || this.StandardTransactionPolicy.MinRelayTxFee == null)
                return Money.Zero;
            return new TxOut(Money.Zero, script).GetDustThreshold(this.StandardTransactionPolicy.MinRelayTxFee);
        }

        /// <summary>
        /// Set transaction policy fluently
        /// </summary>
        /// <param name="policy">The policy</param>
        /// <returns>this</returns>
        public TransactionBuilder SetTransactionPolicy(StandardTransactionPolicy policy)
        {
            this.StandardTransactionPolicy = policy;
            return this;
        }
        public StandardTransactionPolicy StandardTransactionPolicy
        {
            get;
            set;
        }


        private string _OpReturnUser;
        private void AssertOpReturn(string name)
        {
            if(this._OpReturnUser == null)
            {
                this._OpReturnUser = name;
            }
            else
            {
                if(this._OpReturnUser != name)
                    throw new InvalidOperationException("Op return already used for " + this._OpReturnUser);
            }
        }

        public TransactionBuilder Send(BitcoinStealthAddress address, Money amount, Key ephemKey = null)
        {
            if(amount < Money.Zero)
                throw new ArgumentOutOfRangeException("amount", "amount can't be negative");

            if(this._OpReturnUser == null)
                this._OpReturnUser = "Stealth Payment";
            else
                throw new InvalidOperationException("Op return already used for " + this._OpReturnUser);

            this.CurrentGroup.Builders.Add(ctx =>
            {
                StealthPayment payment = address.CreatePayment(ephemKey);
                payment.AddToTransaction(ctx.Transaction, amount);
                return amount;
            });
            return this;
        }

        public TransactionBuilder IssueAsset(IDestination destination, AssetMoney asset)
        {
            return IssueAsset(destination.ScriptPubKey, asset);
        }

        private AssetId _IssuedAsset;

        public TransactionBuilder IssueAsset(Script scriptPubKey, AssetMoney asset)
        {
            AssertOpReturn("Colored Coin");
            if(this._IssuedAsset == null)
                this._IssuedAsset = asset.Id;
            else if(this._IssuedAsset != asset.Id)
                throw new InvalidOperationException("You can issue only one asset type in a transaction");

            this.CurrentGroup.IssuanceBuilders.Add(ctx =>
            {
                ColorMarker marker = ctx.GetColorMarker(true);
                if(ctx.IssuanceCoin == null)
                {
                    IssuanceCoin issuance = ctx.Group.Coins.Values.OfType<IssuanceCoin>().Where(i => i.AssetId == asset.Id).FirstOrDefault();
                    if(issuance == null)
                        throw new InvalidOperationException("No issuance coin for emitting asset found");
                    ctx.IssuanceCoin = issuance;
                    ctx.Transaction.Inputs.Insert(0, new TxIn(issuance.Outpoint));
                    ctx.AdditionalFees -= issuance.Bearer.Amount;
                    if(issuance.DefinitionUrl != null)
                    {
                        marker.SetMetadataUrl(issuance.DefinitionUrl);
                    }
                }

                ctx.Transaction.Outputs.Insert(0, new TxOut(GetDust(scriptPubKey), scriptPubKey));
                marker.Quantities = new[] { checked((ulong)asset.Quantity) }.Concat(marker.Quantities).ToArray();
                ctx.AdditionalFees += ctx.Transaction.Outputs[0].Value;
                return asset;
            });
            return this;
        }

        public TransactionBuilder SendFees(Money fees)
        {
            if(fees == null)
                throw new ArgumentNullException("fees");
            this.CurrentGroup.Builders.Add(ctx => fees);
            this._TotalFee += fees;
            return this;
        }

        private Money _TotalFee = Money.Zero;

        /// <summary>
        /// Split the estimated fees accross the several groups (separated by Then())
        /// </summary>
        /// <param name="feeRate"></param>
        /// <returns></returns>
        public TransactionBuilder SendEstimatedFees(FeeRate feeRate)
        {
            this.FilterUneconomicalCoinsRate = feeRate;
            Money fee = EstimateFees(feeRate);
            SendFees(fee);
            return this;
        }

        /// <summary>
        /// Estimate the fee needed for the transaction, and split among groups according to their fee weight
        /// </summary>
        /// <param name="feeRate"></param>
        /// <returns></returns>
        public TransactionBuilder SendEstimatedFeesSplit(FeeRate feeRate)
        {
            this.FilterUneconomicalCoinsRate = feeRate;
            Money fee = EstimateFees(feeRate);
            SendFeesSplit(fee);
            return this;
        }

        /// <summary>
        /// Send the fee splitted among groups according to their fee weight
        /// </summary>
        /// <param name="fees"></param>
        /// <returns></returns>
        public TransactionBuilder SendFeesSplit(Money fees)
        {
            if(fees == null)
                throw new ArgumentNullException("fees");
            BuilderGroup lastGroup = this.CurrentGroup; //Make sure at least one group exists
            decimal totalWeight = this._BuilderGroups.Select(b => b.FeeWeight).Sum();
            Money totalSent = Money.Zero;
            foreach(BuilderGroup group in this._BuilderGroups)
            {
                Money groupFee = Money.Satoshis((group.FeeWeight / totalWeight) * fees.Satoshi);
                totalSent += groupFee;
                if(this._BuilderGroups.Last() == group)
                {
                    Money leftOver = fees - totalSent;
                    groupFee += leftOver;
                }
                group.Builders.Add(ctx => groupFee);
            }
            return this;
        }


        /// <summary>
        /// If using SendFeesSplit or SendEstimatedFeesSplit, determine the weight this group participate in paying the fees
        /// </summary>
        /// <param name="feeWeight">The weight of fee participation</param>
        /// <returns></returns>
        public TransactionBuilder SetFeeWeight(decimal feeWeight)
        {
            this.CurrentGroup.FeeWeight = feeWeight;
            return this;
        }

        public TransactionBuilder SetChange(IDestination destination, ChangeType changeType = ChangeType.All)
        {
            return SetChange(destination.ScriptPubKey, changeType);
        }

        public TransactionBuilder SetChange(Script scriptPubKey, ChangeType changeType = ChangeType.All)
        {
            if((changeType & ChangeType.Colored) != 0)
            {
                this.CurrentGroup.ChangeScript[(int)ChangeType.Colored] = scriptPubKey;
            }
            if((changeType & ChangeType.Uncolored) != 0)
            {
                this.CurrentGroup.ChangeScript[(int)ChangeType.Uncolored] = scriptPubKey;
            }
            return this;
        }

        public TransactionBuilder SetCoinSelector(ICoinSelector selector)
        {
            if(selector == null)
                throw new ArgumentNullException("selector");
            this.CoinSelector = selector;
            return this;
        }

        /// <summary>
        /// Build the transaction
        /// </summary>
        /// <param name="sign">True if signs all inputs with the available keys</param>
        /// <returns>The transaction</returns>
        /// <exception cref="NBitcoin.NotEnoughFundsException">Not enough funds are available</exception>
        public Transaction BuildTransaction(bool sign)
        {
            return BuildTransaction(sign, SigHash.All);
        }

        /// <summary>
        /// Build the transaction
        /// </summary>
        /// <param name="sign">True if signs all inputs with the available keys</param>
        /// <param name="sigHash">The type of signature</param>
        /// <returns>The transaction</returns>
        /// <exception cref="NBitcoin.NotEnoughFundsException">Not enough funds are available</exception>
        public Transaction BuildTransaction(bool sign, SigHash sigHash)
        {
            var ctx = new TransactionBuildingContext(this);

            if(this._CompletedTransaction != null)
                ctx.Transaction = this.Network.CreateTransaction(this._CompletedTransaction.ToBytes());

            if(this._LockTime != null)
                ctx.Transaction.LockTime = this._LockTime.Value;

            if (this._TimeStamp != null)
                ctx.Transaction.Time = this._TimeStamp.Value;

            foreach (BuilderGroup group in this._BuilderGroups)
            {
                ctx.Group = group;
                ctx.AdditionalBuilders.Clear();
                ctx.AdditionalFees = Money.Zero;

                ctx.ChangeType = ChangeType.Colored;
                foreach(Builder builder in group.IssuanceBuilders)
                    builder(ctx);

                List<KeyValuePair<AssetId, List<Builder>>> buildersByAsset = group.BuildersByAsset.ToList();
                foreach(KeyValuePair<AssetId, List<Builder>> builders in buildersByAsset)
                {
                    IEnumerable<ColoredCoin> coins = group.Coins.Values.OfType<ColoredCoin>().Where(c => c.Amount.Id == builders.Key);

                    ctx.Dust = new AssetMoney(builders.Key);
                    ctx.CoverOnly = null;
                    ctx.ChangeAmount = new AssetMoney(builders.Key);
                    Money btcSpent = BuildTransaction(ctx, group, builders.Value, coins, new AssetMoney(builders.Key))
                        .OfType<IColoredCoin>().Select(c => c.Bearer.Amount).Sum();
                    ctx.AdditionalFees -= btcSpent;
                }

                ctx.AdditionalBuilders.Add(_ => _.AdditionalFees);
                ctx.Dust = GetDust();
                ctx.ChangeAmount = Money.Zero;
                ctx.CoverOnly = group.CoverOnly;
                ctx.ChangeType = ChangeType.Uncolored;
                BuildTransaction(ctx, group, group.Builders, group.Coins.Values.OfType<Coin>().Where(IsEconomical), Money.Zero);
            }

            ctx.Finish();

            if(sign)
            {
                SignTransactionInPlace(ctx.Transaction, sigHash);
            }

            return ctx.Transaction;
        }

        private bool IsEconomical(Coin c)
        {
            if (!this.FilterUneconomicalCoins || this.FilterUneconomicalCoinsRate == null)
                return true;

            int witSize = 0;
            int baseSize = 0;
            EstimateScriptSigSize(c, ref witSize, ref baseSize);
            var vSize = witSize / Transaction.WITNESS_SCALE_FACTOR + baseSize;

            return c.Amount >= this.FilterUneconomicalCoinsRate.GetFee(vSize);
        }

        private IEnumerable<ICoin> BuildTransaction(
            TransactionBuildingContext ctx,
            BuilderGroup group,
            IEnumerable<Builder> builders,
            IEnumerable<ICoin> coins,
            IMoney zero)
        {
            TransactionBuildingContext originalCtx = ctx.CreateMemento();
            Money fees = this._TotalFee + ctx.AdditionalFees;

            // Replace the _SubstractFeeBuilder by another one with the fees substracts
            List<Builder> builderList = builders.ToList();
            for(int i = 0; i < builderList.Count; i++)
            {
                if(builderList[i].Target == this._SubstractFeeBuilder)
                {
                    builderList.Remove(builderList[i]);
                    TxOut newTxOut = this._SubstractFeeBuilder._TxOut.Clone();
                    newTxOut.Value -= fees;
                    builderList.Insert(i, new SendBuilder(newTxOut).Build);
                }
            }
            ////////////////////////////////////////////////////////

            IMoney target = builderList.Concat(ctx.AdditionalBuilders).Select(b => b(ctx)).Sum(zero);
            if(ctx.CoverOnly != null)
            {
                target = ctx.CoverOnly.Add(ctx.ChangeAmount);
            }

            IEnumerable<ICoin> unconsumed = coins.Where(c => ctx.ConsumedCoins.All(cc => cc.Outpoint != c.Outpoint));
            IEnumerable<ICoin> selection = this.CoinSelector.Select(unconsumed, target);
            if(selection == null)
            {
                throw new NotEnoughFundsException("Not enough funds to cover the target",
                    group.Name,
                    target.Sub(unconsumed.Select(u => u.Amount).Sum(zero))
                );
            }

            IMoney total = selection.Select(s => s.Amount).Sum(zero);
            IMoney change = total.Sub(target);
            if(change.CompareTo(zero) == -1)
            {
                throw new NotEnoughFundsException("Not enough funds to cover the target",
                    group.Name,
                    change.Negate()
                );
            }

            if(change.CompareTo(ctx.Dust) == 1)
            {
                Script changeScript = group.ChangeScript[(int)ctx.ChangeType];

                if(changeScript == null)
                    throw new InvalidOperationException("A change address should be specified (" + ctx.ChangeType + ")");

                if(!(ctx.Dust is Money) || change.CompareTo(GetDust(changeScript)) == 1)
                {
                    ctx.RestoreMemento(originalCtx);
                    ctx.ChangeAmount = change;
                    try
                    {
                        return BuildTransaction(ctx, group, builders, coins, zero);
                    }
                    finally
                    {
                        ctx.ChangeAmount = zero;
                    }
                }
            }

            foreach(ICoin coin in selection)
            {
                ctx.ConsumedCoins.Add(coin);
                TxIn input = ctx.Transaction.Inputs.FirstOrDefault(i => i.PrevOut == coin.Outpoint);
                if(input == null)
                    input = ctx.Transaction.AddInput(new TxIn(coin.Outpoint));
                if(this._LockTime != null && !ctx.NonFinalSequenceSet)
                {
                    input.Sequence = 0;
                    ctx.NonFinalSequenceSet = true;
                }
            }

            return selection;
        }

        public Transaction SignTransaction(Transaction transaction, SigHash sigHash)
        {
            Transaction tx = this.Network.CreateTransaction(transaction.ToBytes());
            SignTransactionInPlace(tx, sigHash);
            return tx;
        }

        public Transaction SignTransaction(Transaction transaction)
        {
            return SignTransaction(transaction, SigHash.All);
        }

        public Transaction SignTransactionInPlace(Transaction transaction)
        {
            return SignTransactionInPlace(transaction, SigHash.All);
        }

        public Transaction SignTransactionInPlace(Transaction transaction, SigHash sigHash)
        {
            var ctx = new TransactionSigningContext(this, transaction)
            {
                SigHash = sigHash
            };

            foreach (IndexedTxIn input in transaction.Inputs.AsIndexedInputs())
            {
                ICoin coin = FindSignableCoin(input);
                if(coin != null)
                {
                    Sign(ctx, coin, input);
                }
            }
            return transaction;
        }

        public ICoin FindSignableCoin(IndexedTxIn txIn)
        {
            ICoin coin = FindCoin(txIn.PrevOut);

            if(coin is IColoredCoin)
                coin = ((IColoredCoin)coin).Bearer;

            if(coin == null || coin is ScriptCoin || coin is StealthCoin)
                return coin;

            TxDestination hash = ScriptCoin.GetRedeemHash(this.Network, coin.TxOut.ScriptPubKey);
            if(hash != null)
            {
                Script redeem = this._ScriptPubKeyToRedeem.TryGet(coin.TxOut.ScriptPubKey);
                if(redeem != null && PayToWitScriptHashTemplate.Instance.CheckScriptPubKey(redeem))
                    redeem = this._ScriptPubKeyToRedeem.TryGet(redeem);
                if(redeem == null)
                {
                    if(hash is WitScriptId)
                        redeem = PayToWitScriptHashTemplate.Instance.ExtractWitScriptParameters(txIn.WitScript, (WitScriptId)hash);
                    if(hash is ScriptId)
                    {
                        PayToScriptHashSigParameters parameters = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(this.Network, txIn.ScriptSig, (ScriptId)hash);
                        if(parameters != null)
                            redeem = parameters.RedeemScript;
                    }
                }
                if(redeem != null)
                    return new ScriptCoin(coin, redeem);
            }
            return coin;
        }

        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx)
        {
            TransactionPolicyError[] errors;
            return Verify(tx, null as Money, out errors);
        }
        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <param name="expectedFees">The expected fees (more or less 10%)</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx, Money expectedFees)
        {
            TransactionPolicyError[] errors;
            return Verify(tx, expectedFees, out errors);
        }

        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <param name="expectedFeeRate">The expected fee rate</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx, FeeRate expectedFeeRate)
        {
            TransactionPolicyError[] errors;
            return Verify(tx, expectedFeeRate, out errors);
        }

        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <param name="errors">Detected errors</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx, out TransactionPolicyError[] errors)
        {
            return Verify(tx, null as Money, out errors);
        }
        /// <summary>
        /// Verify that a transaction is fully signed, have enough fees, and follow the Standard and Miner Transaction Policy rules
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <param name="expectedFees">The expected fees (more or less 10%)</param>
        /// <param name="errors">Detected errors</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx, Money expectedFees, out TransactionPolicyError[] errors)
        {
            if(tx == null)
                throw new ArgumentNullException("tx");
            ICoin[] coins = tx.Inputs.Select(i => FindCoin(i.PrevOut)).Where(c => c != null).ToArray();
            var exceptions = new List<TransactionPolicyError>();
            TransactionPolicyError[] policyErrors = MinerTransactionPolicy.Instance.Check(tx, coins);
            exceptions.AddRange(policyErrors);
            policyErrors = this.StandardTransactionPolicy.Check(tx, coins);
            exceptions.AddRange(policyErrors);
            if(expectedFees != null)
            {
                Money fees = tx.GetFee(coins);
                if(fees != null)
                {
                    Money margin = Money.Zero;
                    if(this.DustPrevention)
                        margin = GetDust() * 2;
                    if(!fees.Almost(expectedFees, margin))
                        exceptions.Add(new NotEnoughFundsPolicyError("Fees different than expected", expectedFees - fees));
                }
            }
            errors = exceptions.ToArray();
            return errors.Length == 0;
        }
        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">The transaction to check</param>
        /// <param name="expectedFeeRate">The expected fee rate</param>
        /// <param name="errors">Detected errors</param>
        /// <returns>True if no error</returns>
        public bool Verify(Transaction tx, FeeRate expectedFeeRate, out TransactionPolicyError[] errors)
        {
            if(tx == null)
                throw new ArgumentNullException("tx");
            return Verify(tx, expectedFeeRate == null ? null : expectedFeeRate.GetFee(tx), out errors);
        }
        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">he transaction to check</param>
        /// <param name="expectedFeeRate">The expected fee rate</param>
        /// <returns>Detected errors</returns>
        public TransactionPolicyError[] Check(Transaction tx, FeeRate expectedFeeRate)
        {
            return Check(tx, expectedFeeRate == null ? null : expectedFeeRate.GetFee(tx));
        }
        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">he transaction to check</param>
        /// <param name="expectedFee">The expected fee</param>
        /// <returns>Detected errors</returns>
        public TransactionPolicyError[] Check(Transaction tx, Money expectedFee)
        {
            TransactionPolicyError[] errors;
            Verify(tx, expectedFee, out errors);
            return errors;
        }
        /// <summary>
        /// Verify that a transaction is fully signed and have enough fees
        /// </summary>
        /// <param name="tx">he transaction to check</param>
        /// <returns>Detected errors</returns>
        public TransactionPolicyError[] Check(Transaction tx)
        {
            return Check(tx, null as Money);
        }

        private CoinNotFoundException CoinNotFound(IndexedTxIn txIn)
        {
            return new CoinNotFoundException(txIn);
        }


        public ICoin FindCoin(OutPoint outPoint)
        {
            ICoin result = this._BuilderGroups.Select(c => c.Coins.TryGet(outPoint)).FirstOrDefault(r => r != null);
            if(result == null && this.CoinFinder != null)
                result = this.CoinFinder(outPoint);
            return result;
        }

        /// <summary>
        /// Find spent coins of a transaction
        /// </summary>
        /// <param name="tx">The transaction</param>
        /// <returns>Array of size tx.Input.Count, if a coin is not fund, a null coin is returned.</returns>
        public ICoin[] FindSpentCoins(Transaction tx)
        {
            return
                tx
                .Inputs
                .Select(i => FindCoin(i.PrevOut))
                .ToArray();
        }

        /// <summary>
        /// Estimate the physical size of the transaction
        /// </summary>
        /// <param name="tx">The transaction to be estimated</param>
        /// <returns></returns>
        public int EstimateSize(Transaction tx)
        {
            return EstimateSize(tx, false);
        }

        /// <summary>
        /// Estimate the size of the transaction
        /// </summary>
        /// <param name="tx">The transaction to be estimated</param>
        /// <param name="virtualSize">If true, returns the size on which fee calculation are based, else returns the physical byte size</param>
        /// <returns></returns>
        public int EstimateSize(Transaction tx, bool virtualSize)
        {
            if (tx == null)
                throw new ArgumentNullException("tx");

            Transaction clone = this.Network.CreateTransaction(tx.ToHex());
            clone.Inputs.Clear();
            int baseSize = clone.GetSerializedSize();

            int witSize = 0;
            if (tx.HasWitness)
                witSize += 2;
            foreach (var txin in tx.Inputs.AsIndexedInputs())
            {
                ICoin coin = FindSignableCoin(txin) ?? FindCoin(txin.PrevOut);
                if (coin == null)
                    throw CoinNotFound(txin);
                EstimateScriptSigSize(coin, ref witSize, ref baseSize);
                baseSize += 41;
            }

            return (virtualSize ? witSize / Transaction.WITNESS_SCALE_FACTOR + baseSize : witSize + baseSize);
        }

        private void EstimateScriptSigSize(ICoin coin, ref int witSize, ref int baseSize)
        {
            if (coin is IColoredCoin)
                coin = ((IColoredCoin)coin).Bearer;

            if (coin is ScriptCoin scriptCoin)
            {
                Script p2sh = scriptCoin.GetP2SHRedeem();
                if (p2sh != null)
                {
                    coin = new Coin(scriptCoin.Outpoint, new TxOut(scriptCoin.Amount, p2sh));
                    baseSize += new Script(Op.GetPushOp(p2sh.ToBytes(true))).Length;
                    if (scriptCoin.RedeemType == RedeemType.WitnessV0)
                    {
                        coin = new ScriptCoin(coin, scriptCoin.Redeem);
                    }
                }

                if (scriptCoin.RedeemType == RedeemType.WitnessV0)
                {
                    witSize += new Script(Op.GetPushOp(scriptCoin.Redeem.ToBytes(true))).Length;
                }
            }

            Script scriptPubkey = coin.GetScriptCode(this.Network);
            int scriptSigSize = -1;
            foreach(BuilderExtension extension in this.Extensions)
            {
                if(extension.CanEstimateScriptSigSize(this.Network, scriptPubkey))
                {
                    scriptSigSize = extension.EstimateScriptSigSize(this.Network, scriptPubkey);
                    break;
                }
            }

            if (scriptSigSize == -1)
                scriptSigSize += coin.TxOut.ScriptPubKey.Length; //Using heurestic to approximate size of unknown scriptPubKey

            if(coin.GetHashVersion(this.Network) == HashVersion.Witness)
                witSize += scriptSigSize + 1; //Account for the push
            if(coin.GetHashVersion(this.Network) == HashVersion.Original)
                baseSize += scriptSigSize;
        }

        /// <summary>
        /// Estimate fees of the built transaction
        /// </summary>
        /// <param name="feeRate">Fee rate</param>
        /// <returns></returns>
        public Money EstimateFees(FeeRate feeRate)
        {
            if(feeRate == null)
                throw new ArgumentNullException("feeRate");

            int builderCount = this.CurrentGroup.Builders.Count;
            Money feeSent = Money.Zero;
            try
            {
                while(true)
                {
                    Transaction tx = BuildTransaction(false);
                    Money shouldSend = EstimateFees(tx, feeRate);
                    Money delta = shouldSend - feeSent;
                    if(delta <= Money.Zero)
                        break;
                    SendFees(delta);
                    feeSent += delta;
                }
            }
            finally
            {
                while(this.CurrentGroup.Builders.Count != builderCount)
                {
                    this.CurrentGroup.Builders.RemoveAt(this.CurrentGroup.Builders.Count - 1);
                }
                this._TotalFee -= feeSent;
            }
            return feeSent;
        }

        /// <summary>
        /// Estimate fees of an unsigned transaction
        /// </summary>
        /// <param name="tx"></param>
        /// <param name="feeRate">Fee rate</param>
        /// <returns></returns>
        public Money EstimateFees(Transaction tx, FeeRate feeRate)
        {
            if(tx == null)
                throw new ArgumentNullException("tx");
            if(feeRate == null)
                throw new ArgumentNullException("feeRate");

            int estimation = EstimateSize(tx, true);
            return feeRate.GetFee(estimation);
        }

        private void Sign(TransactionSigningContext ctx, ICoin coin, IndexedTxIn txIn)
        {
            TxIn input = txIn.TxIn;
            if(coin is StealthCoin)
            {
                var stealthCoin = (StealthCoin)coin;
                Key scanKey = FindKey(ctx, stealthCoin.Address.ScanPubKey.ScriptPubKey);
                if(scanKey == null)
                    throw new KeyNotFoundException("Scan key for decrypting StealthCoin not found");
                Key[] spendKeys = stealthCoin.Address.SpendPubKeys.Select(p => FindKey(ctx, p.ScriptPubKey)).Where(p => p != null).ToArray();
                ctx.AdditionalKeys.AddRange(stealthCoin.Uncover(spendKeys, scanKey));
                var normalCoin = new Coin(coin.Outpoint, coin.TxOut);
                if(stealthCoin.Redeem != null)
                    normalCoin = normalCoin.ToScriptCoin(stealthCoin.Redeem);
                coin = normalCoin;
            }
            Script scriptSig = CreateScriptSig(ctx, coin, txIn);
            if(scriptSig == null)
                return;
            var scriptCoin = coin as ScriptCoin;

            Script signatures = null;
            if(coin.GetHashVersion(this.Network) == HashVersion.Witness)
            {
                signatures = txIn.WitScript;
                if(scriptCoin != null)
                {
                    if(scriptCoin.IsP2SH)
                        txIn.ScriptSig = Script.Empty;
                    if(scriptCoin.RedeemType == RedeemType.WitnessV0)
                        signatures = RemoveRedeem(signatures);
                }
            }
            else
            {
                signatures = txIn.ScriptSig;
                if(scriptCoin != null && scriptCoin.RedeemType == RedeemType.P2SH)
                    signatures = RemoveRedeem(signatures);
            }


            signatures = CombineScriptSigs(coin, scriptSig, signatures);

            if(coin.GetHashVersion(this.Network) == HashVersion.Witness)
            {
                txIn.WitScript = signatures;
                if(scriptCoin != null)
                {
                    if(scriptCoin.IsP2SH)
                        txIn.ScriptSig = new Script(Op.GetPushOp(scriptCoin.GetP2SHRedeem().ToBytes(true)));
                    if(scriptCoin.RedeemType == RedeemType.WitnessV0)
                        txIn.WitScript = txIn.WitScript + new WitScript(Op.GetPushOp(scriptCoin.Redeem.ToBytes(true)));
                }
            }
            else
            {
                txIn.ScriptSig = signatures;
                if(scriptCoin != null && scriptCoin.RedeemType == RedeemType.P2SH)
                {
                    txIn.ScriptSig = input.ScriptSig + Op.GetPushOp(scriptCoin.GetP2SHRedeem().ToBytes(true));
                }
            }
        }


        private static Script RemoveRedeem(Script script)
        {
            if(script == Script.Empty)
                return script;
            Op[] ops = script.ToOps().ToArray();
            return new Script(ops.Take(ops.Length - 1));
        }

        private Script CombineScriptSigs(ICoin coin, Script a, Script b)
        {
            Script scriptPubkey = coin.GetScriptCode(this.Network);
            if(Script.IsNullOrEmpty(a))
                return b ?? Script.Empty;
            if(Script.IsNullOrEmpty(b))
                return a ?? Script.Empty;

            foreach(BuilderExtension extension in this.Extensions)
            {
                if(extension.CanCombineScriptSig(this.Network, scriptPubkey, a, b))
                {
                    return extension.CombineScriptSig(this.Network, scriptPubkey, a, b);
                }
            }
            return a.Length > b.Length ? a : b; //Heurestic
        }

        private Script CreateScriptSig(TransactionSigningContext ctx, ICoin coin, IndexedTxIn txIn)
        {
            Script scriptPubKey = coin.GetScriptCode(this.Network);
            var keyRepo = new TransactionBuilderKeyRepository(this, ctx);
            var signer = new TransactionBuilderSigner(this, coin, ctx.SigHash, txIn);

            var signer2 = new KnownSignatureSigner(this, this._KnownSignatures, coin, ctx.SigHash, txIn);

            foreach(BuilderExtension extension in this.Extensions)
            {
                if(extension.CanGenerateScriptSig(this.Network, scriptPubKey))
                {
                    Script scriptSig1 = extension.GenerateScriptSig(this.Network, scriptPubKey, keyRepo, signer);
                    Script scriptSig2 = extension.GenerateScriptSig(this.Network, scriptPubKey, signer2, signer2);
                    if (scriptSig2 != null)
                    {
                        scriptSig2 = signer2.ReplaceDummyKeys(scriptSig2);
                    }
                    if (scriptSig1 != null && scriptSig2 != null && extension.CanCombineScriptSig(this.Network, scriptPubKey, scriptSig1, scriptSig2))
                    {
                        Script combined = extension.CombineScriptSig(this.Network, scriptPubKey, scriptSig1, scriptSig2);
                        return combined;
                    }
                    return scriptSig1 ?? scriptSig2;
                }
            }

            throw new NotSupportedException("Unsupported scriptPubKey");
        }

        private List<Tuple<PubKey, ECDSASignature>> _KnownSignatures = new List<Tuple<PubKey, ECDSASignature>>();

        private Key FindKey(TransactionSigningContext ctx, Script scriptPubKey)
        {
            Key key = this._Keys
                .Concat(ctx.AdditionalKeys)
                .FirstOrDefault(k => IsCompatibleKey(k.PubKey, scriptPubKey));
            if(key == null && this.KeyFinder != null)
            {
                key = this.KeyFinder(scriptPubKey);
            }
            return key;
        }

        private static bool IsCompatibleKey(PubKey k, Script scriptPubKey)
        {
            return k.ScriptPubKey == scriptPubKey ||  //P2PK
                    k.Hash.ScriptPubKey == scriptPubKey || //P2PKH
                    k.ScriptPubKey.Hash.ScriptPubKey == scriptPubKey || //P2PK P2SH
                    k.Hash.ScriptPubKey.Hash.ScriptPubKey == scriptPubKey; //P2PKH P2SH
        }

        /// <summary>
        /// Create a new participant in the transaction with its own set of coins and keys
        /// </summary>
        /// <returns></returns>
        public TransactionBuilder Then()
        {
            this._CurrentGroup = null;
            return this;
        }

        /// <summary>
        /// Switch to another participant in the transaction, or create a new one if it is not found.
        /// </summary>
        /// <returns></returns>
        public TransactionBuilder Then(string groupName)
        {
            BuilderGroup group = this._BuilderGroups.FirstOrDefault(g => g.Name == groupName);
            if(group == null)
            {
                group = new BuilderGroup(this);
                this._BuilderGroups.Add(group);
                group.Name = groupName;
            }

            this._CurrentGroup = group;
            return this;
        }

        /// <summary>
        /// Specify the amount of money to cover txouts, if not specified all txout will be covered
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public TransactionBuilder CoverOnly(Money amount)
        {
            this.CurrentGroup.CoverOnly = amount;
            return this;
        }


        private Transaction _CompletedTransaction;

        /// <summary>
        /// Allows to keep building on the top of a partially built transaction
        /// </summary>
        /// <param name="transaction">Transaction to complete</param>
        /// <returns></returns>
        public TransactionBuilder ContinueToBuild(Transaction transaction)
        {
            if(this._CompletedTransaction != null)
                throw new InvalidOperationException("Transaction to complete already set");

            this._CompletedTransaction = this.Network.CreateTransaction(transaction.ToHex());

            return this;
        }

        /// <summary>
        /// Will cover the remaining amount of TxOut of a partially built transaction (to call after ContinueToBuild)
        /// </summary>
        /// <returns></returns>
        public TransactionBuilder CoverTheRest()
        {
            if(this._CompletedTransaction == null)
                throw new InvalidOperationException("A partially built transaction should be specified by calling ContinueToBuild");

            Money spent = this._CompletedTransaction.Inputs.AsIndexedInputs().Select(txin =>
            {
                ICoin c = FindCoin(txin.PrevOut);
                if(c == null)
                    throw CoinNotFound(txin);
                if(!(c is Coin))
                    return null;
                return (Coin)c;
            })
                    .Where(c => c != null)
                    .Select(c => c.Amount)
                    .Sum();

            Money toComplete = this._CompletedTransaction.TotalOut - spent;
            this.CurrentGroup.Builders.Add(ctx =>
            {
                if(toComplete < Money.Zero)
                    return Money.Zero;
                return toComplete;
            });
            return this;
        }

        public TransactionBuilder AddCoins(Transaction transaction)
        {
            uint256 txId = transaction.GetHash();
            AddCoins(transaction.Outputs.Select((o, i) => new Coin(txId, (uint)i, o.Value, o.ScriptPubKey)).ToArray());
            return this;
        }

        private Dictionary<Script, Script> _ScriptPubKeyToRedeem = new Dictionary<Script, Script>();
        public TransactionBuilder AddKnownRedeems(params Script[] knownRedeems)
        {
            foreach(Script redeem in knownRedeems)
            {
                this._ScriptPubKeyToRedeem.AddOrReplace(redeem.WitHash.ScriptPubKey.Hash.ScriptPubKey, redeem); //Might be P2SH(PWSH)
                this._ScriptPubKeyToRedeem.AddOrReplace(redeem.Hash.ScriptPubKey, redeem); //Might be P2SH
                this._ScriptPubKeyToRedeem.AddOrReplace(redeem.WitHash.ScriptPubKey, redeem); //Might be PWSH
            }
            return this;
        }

        public Transaction CombineSignatures(params Transaction[] transactions)
        {
            if(transactions.Length == 1)
                return transactions[0];

            if(transactions.Length == 0)
                return null;

            Transaction tx = this.Network.CreateTransaction(transactions[0].ToHex());
            for(int i = 1; i < transactions.Length; i++)
            {
                Transaction signed = transactions[i];
                tx = CombineSignaturesCore(tx, signed);
            }
            return tx;
        }

        private readonly List<BuilderExtension> _Extensions = new List<BuilderExtension>();
        public List<BuilderExtension> Extensions
        {
            get
            {
                return this._Extensions;
            }
        }

        private Transaction CombineSignaturesCore(Transaction signed1, Transaction signed2)
        {
            if(signed1 == null)
                return signed2;

            if(signed2 == null)
                return signed1;

            Transaction tx = this.Network.CreateTransaction(signed1.ToHex());
            for(int i = 0; i < tx.Inputs.Count; i++)
            {
                if(i >= signed2.Inputs.Count)
                    break;

                TxIn txIn = tx.Inputs[i];

                ICoin coin = FindCoin(txIn.PrevOut);
                Script scriptPubKey = coin == null
                    ? (DeduceScriptPubKey(txIn.ScriptSig) ?? DeduceScriptPubKey(signed2.Inputs[i].ScriptSig))
                    : coin.TxOut.ScriptPubKey;

                Money amount = null;
                if(coin != null)
                    amount = coin is IColoredCoin ? ((IColoredCoin)coin).Bearer.Amount : ((Coin)coin).Amount;
                ScriptSigs result = Script.CombineSignatures(
                                    this.Network,
                                    scriptPubKey,
                                    new TransactionChecker(tx, i, amount),
                                     GetScriptSigs(signed1.Inputs.AsIndexedInputs().Skip(i).First()),
                                     GetScriptSigs(signed2.Inputs.AsIndexedInputs().Skip(i).First()));
                IndexedTxIn input = tx.Inputs.AsIndexedInputs().Skip(i).First();
                input.WitScript = result.WitSig;
                input.ScriptSig = result.ScriptSig;
            }
            return tx;
        }

        private ScriptSigs GetScriptSigs(IndexedTxIn indexedTxIn)
        {
            return new ScriptSigs()
            {
                ScriptSig = indexedTxIn.ScriptSig,
                WitSig = indexedTxIn.WitScript
            };
        }

        private Script DeduceScriptPubKey(Script scriptSig)
        {
            PayToScriptHashSigParameters p2sh = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(this.Network, scriptSig);
            if(p2sh != null && p2sh.RedeemScript != null)
            {
                return p2sh.RedeemScript.Hash.ScriptPubKey;
            }
            foreach(BuilderExtension extension in this.Extensions)
            {
                if(extension.CanDeduceScriptPubKey(this.Network, scriptSig))
                {
                    return extension.DeduceScriptPubKey(this.Network, scriptSig);
                }
            }
            return null;
        }
    }

    public class CoinNotFoundException : KeyNotFoundException
    {
        public CoinNotFoundException(IndexedTxIn txIn)
            : base("No coin matching " + txIn.PrevOut + " was found")
        {
            this._OutPoint = txIn.PrevOut;
            this._InputIndex = txIn.Index;
        }

        private readonly OutPoint _OutPoint;
        public OutPoint OutPoint
        {
            get
            {
                return this._OutPoint;
            }
        }

        private readonly uint _InputIndex;
        public uint InputIndex
        {
            get
            {
                return this._InputIndex;
            }
        }
    }
}
