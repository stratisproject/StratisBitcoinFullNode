﻿using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NBitcoin.OpenAsset
{
    public class ColoredEntry : IBitcoinSerializable
    {
        public ColoredEntry()
        {

        }
        public ColoredEntry(uint index, AssetMoney asset)
        {
            if(asset == null)
                throw new ArgumentNullException("asset");
            this.Index = index;
            this.Asset = asset;
        }

        private uint _Index;
        public uint Index
        {
            get
            {
                return this._Index;
            }
            set
            {
                this._Index = value;
            }
        }

        private AssetMoney _Asset = new AssetMoney(new AssetId(new uint160(0)));
        public AssetMoney Asset
        {
            get
            {
                return this._Asset;
            }
            set
            {
                this._Asset = value;
            }
        }
#region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWriteAsVarInt(ref this._Index);
            if(stream.Serializing)
            {
                byte[] assetId = this.Asset.Id.ToBytes();
                stream.ReadWrite(ref assetId);
                long quantity = this.Asset.Quantity;
                stream.ReadWrite(ref quantity);
            }
            else
            {
                var assetId = new byte[20];
                stream.ReadWrite(ref assetId);
                long quantity = 0;
                stream.ReadWrite(ref quantity);
                this.Asset = new AssetMoney(new AssetId(assetId), quantity);
            }
        }

#endregion

        public override string ToString()
        {
            if(this.Asset == null)
                return "[" + this.Index + "]";
            else
                return "[" + this.Index + "] " + this.Asset;
        }
    }
    public class ColoredTransaction : IBitcoinSerializable
    {
        public static Task<ColoredTransaction> FetchColorsAsync(Transaction tx, IColoredTransactionRepository repo)
        {
            return FetchColorsAsync(null, tx, repo);
        }
        public static ColoredTransaction FetchColors(Transaction tx, IColoredTransactionRepository repo)
        {
            return FetchColors(null, tx, repo);
        }
        public static Task<ColoredTransaction> FetchColorsAsync(uint256 txId, IColoredTransactionRepository repo)
        {
            return FetchColorsAsync(txId, null, repo);
        }
        public static ColoredTransaction FetchColors(uint256 txId, IColoredTransactionRepository repo)
        {
            return FetchColors(txId, null, repo);
        }
        public static ColoredTransaction FetchColors(uint256 txId, Transaction tx, IColoredTransactionRepository repo)
        {
            return FetchColorsAsync(txId, tx, repo).GetAwaiter().GetResult();
        }

        private class ColoredFrame
        {
            public uint256 TransactionId
            {
                get;
                set;
            }
            public ColoredTransaction[] PreviousTransactions
            {
                get;
                set;
            }

            public Transaction Transaction
            {
                get;
                set;
            }
        }

        public static async Task<ColoredTransaction> FetchColorsAsync(uint256 txId, Transaction tx, IColoredTransactionRepository repo)
        {
            if(repo == null)
                throw new ArgumentNullException("repo");
            if(txId == null)
            {
                if(tx == null)
                    throw new ArgumentException("txId or tx should be different of null");
                txId = tx.GetHash();
            }
            //The following code is to prevent recursion of FetchColors that would fire a StackOverflow if the origin of traded asset were deep in the transaction dependency tree
            ColoredTransaction colored = await repo.GetAsync(txId).ConfigureAwait(false);
            if(colored != null)
                return colored;

            var frames = new Stack<ColoredFrame>();
            var coloreds = new Stack<ColoredTransaction>();
            frames.Push(new ColoredFrame()
            {
                TransactionId = txId,
                Transaction = tx
            });
            while(frames.Count != 0)
            {
                ColoredFrame frame = frames.Pop();
                colored = frame.PreviousTransactions != null ? null : await repo.GetAsync(frame.TransactionId).ConfigureAwait(false); //Already known
                if(colored != null)
                {
                    coloreds.Push(colored);
                    continue;
                }
                frame.Transaction = frame.Transaction ?? await repo.Transactions.GetAsync(frame.TransactionId).ConfigureAwait(false);
                if(frame.Transaction == null)
                    throw new TransactionNotFoundException("Transaction " + frame.TransactionId + " not found in transaction repository", frame.TransactionId);
                if(frame.PreviousTransactions == null)
                {
                    if(frame.Transaction.IsCoinBase ||
                        (!frame.Transaction.HasValidColoredMarker()
                    && frame.TransactionId != txId)) //We care about destroyed asset, if this is the requested transaction
                    {
                        coloreds.Push(new ColoredTransaction());
                        continue;
                    }
                    frame.PreviousTransactions = new ColoredTransaction[frame.Transaction.Inputs.Count];
                    await BulkLoadIfCached(frame.Transaction, repo).ConfigureAwait(false);
                    frames.Push(frame);
                    for(int i = 0; i < frame.Transaction.Inputs.Count; i++)
                    {
                        frames.Push(new ColoredFrame()
                        {
                            TransactionId = frame.Transaction.Inputs[i].PrevOut.Hash
                        });
                    }
                    frame.Transaction = frame.TransactionId == txId ? frame.Transaction : null; //Do not waste memory, will refetch later
                    continue;
                }
                else
                {
                    for(int i = 0; i < frame.Transaction.Inputs.Count; i++)
                    {
                        frame.PreviousTransactions[i] = coloreds.Pop();
                    }
                }

                Script issuanceScriptPubkey = null;
                if(HasIssuance(frame.Transaction))
                {
                    TxIn txIn = frame.Transaction.Inputs[0];
                    Transaction previous = await repo.Transactions.GetAsync(txIn.PrevOut.Hash).ConfigureAwait(false);
                    if(previous == null)
                    {
                        throw new TransactionNotFoundException("An open asset transaction is issuing assets, but it needs a parent transaction in the TransactionRepository to know the address of the issued asset (missing : " + txIn.PrevOut.Hash + ")", txIn.PrevOut.Hash);
                    }
                    if(txIn.PrevOut.N < previous.Outputs.Count)
                        issuanceScriptPubkey = previous.Outputs[txIn.PrevOut.N].ScriptPubKey;
                }

                var spentCoins = new List<ColoredCoin>();
                for(int i = 0; i < frame.Transaction.Inputs.Count; i++)
                {
                    TxIn txIn = frame.Transaction.Inputs[i];
                    ColoredEntry entry = frame.PreviousTransactions[i].GetColoredEntry(txIn.PrevOut.N);
                    if(entry != null)
                        spentCoins.Add(new ColoredCoin(entry.Asset, new Coin(txIn.PrevOut, new TxOut())));
                }
                colored = new ColoredTransaction(frame.TransactionId, frame.Transaction, spentCoins.ToArray(), issuanceScriptPubkey);
                coloreds.Push(colored);
                await repo.PutAsync(frame.TransactionId, colored).ConfigureAwait(false);
            }
            if(coloreds.Count != 1)
                throw new InvalidOperationException("Colored stack length != 1, this is a NBitcoin bug, please contact us.");
            return coloreds.Pop();
        }

        private static async Task<bool> BulkLoadIfCached(Transaction transaction, IColoredTransactionRepository repo)
        {
            if(!(repo is CachedColoredTransactionRepository))
                return false;
            bool hasIssuance = HasIssuance(transaction);
            repo = new NoDuplicateColoredTransactionRepository(repo); //prevent from having concurrent request to the same transaction id
            Task[] all = Enumerable
                .Range(0, transaction.Inputs.Count)
                .Select(async i =>
                {
                    uint256 txId = transaction.Inputs[i].PrevOut.Hash;
                    ColoredTransaction result = await repo.GetAsync(txId).ConfigureAwait(false);
                    if(result == null || (i == 0 && hasIssuance))
                        await repo.Transactions.GetAsync(txId).ConfigureAwait(false);
                })
                .ToArray();
            await Task.WhenAll(all).ConfigureAwait(false);
            return true;
        }

        public ColoredTransaction(Transaction tx, ColoredCoin[] spentCoins, Script issuanceScriptPubkey)
            : this(tx.GetHash(), tx, spentCoins, issuanceScriptPubkey)
        {

        }


        public ColoredEntry GetColoredEntry(uint n)
        {
            return this.Issuances
                .Concat(this.Transfers)
                .FirstOrDefault(i => i.Index == n);
        }

        public static bool HasIssuance(Transaction tx)
        {
            if(tx.Inputs.Count == 0)
                return false;
            uint markerPos = 0;
            ColorMarker marker = ColorMarker.Get(tx, out markerPos);
            if(marker == null)
            {
                return false;
            }
            if(!marker.HasValidQuantitiesCount(tx))
            {
                return false;
            }

            for(uint i = 0; i < markerPos; i++)
            {
                ulong quantity = i >= marker.Quantities.Length ? 0 : marker.Quantities[i];
                if(quantity != 0)
                    return true;
            }
            return false;
        }

        public ColoredTransaction()
        {
            this.Issuances = new List<ColoredEntry>();
            this.Transfers = new List<ColoredEntry>();
            this.Inputs = new List<ColoredEntry>();
        }

        public ColoredTransaction(uint256 txId, Transaction tx, ColoredCoin[] spentCoins, Script issuanceScriptPubkey)
            : this()
        {
            if(tx == null)
                throw new ArgumentNullException("tx");
            if(spentCoins == null)
                throw new ArgumentNullException("spentCoins");
            if(tx.IsCoinBase || tx.Inputs.Count == 0)
                return;
            txId = txId ?? tx.GetHash();

            var previousAssetQueue = new Queue<ColoredEntry>();
            for(uint i = 0; i < tx.Inputs.Count; i++)
            {
                TxIn txin = tx.Inputs[i];
                ColoredCoin prevAsset = spentCoins.FirstOrDefault(s => s.Outpoint == txin.PrevOut);
                if(prevAsset != null)
                {
                    var input = new ColoredEntry()
                    {
                        Index = i,
                        Asset = prevAsset.Amount
                    };
                    previousAssetQueue.Enqueue(input);
                    this.Inputs.Add(input);
                }
            }

            uint markerPos = 0;
            ColorMarker marker = ColorMarker.Get(tx, out markerPos);
            if(marker == null)
            {
                return;
            }

            this.Marker = marker;
            if(!marker.HasValidQuantitiesCount(tx))
            {
                return;
            }

            AssetId issuedAsset = null;
            for(uint i = 0; i < markerPos; i++)
            {
                var entry = new ColoredEntry();
                entry.Index = i;
                entry.Asset = new AssetMoney(entry.Asset.Id, i >= marker.Quantities.Length ? 0 : marker.Quantities[i]);
                if(entry.Asset.Quantity == 0)
                    continue;

                if(issuedAsset == null)
                {
                    TxIn txIn = tx.Inputs.FirstOrDefault();
                    if(txIn == null)
                        continue;
                    if(issuanceScriptPubkey == null)
                        throw new ArgumentException("The transaction has an issuance detected, but issuanceScriptPubkey is null.", "issuanceScriptPubkey");
                    issuedAsset = issuanceScriptPubkey.Hash.ToAssetId();
                }
                entry.Asset = new AssetMoney(issuedAsset, entry.Asset.Quantity);
                this.Issuances.Add(entry);
            }

            long used = 0;
            for(uint i = markerPos + 1; i < tx.Outputs.Count; i++)
            {
                var entry = new ColoredEntry();
                entry.Index = i;
                //If there are less items in the  asset quantity list  than the number of colorable outputs (all the outputs except the marker output), the outputs in excess receive an asset quantity of zero.
                entry.Asset = new AssetMoney(entry.Asset.Id, (i - 1) >= marker.Quantities.Length ? 0 : marker.Quantities[i - 1]);
                if(entry.Asset.Quantity == 0)
                    continue;

                //If there are less asset units in the input sequence than in the output sequence, the transaction is considered invalid and all outputs are uncolored. 
                if(previousAssetQueue.Count == 0)
                {
                    this.Transfers.Clear();
                    this.Issuances.Clear();
                    return;
                }
                entry.Asset = new AssetMoney(previousAssetQueue.Peek().Asset.Id, entry.Asset.Quantity);
                long remaining = entry.Asset.Quantity;
                while(remaining != 0)
                {
                    if(previousAssetQueue.Count == 0 || previousAssetQueue.Peek().Asset.Id != entry.Asset.Id)
                    {
                        this.Transfers.Clear();
                        this.Issuances.Clear();
                        return;
                    }
                    long assertPart = Math.Min(previousAssetQueue.Peek().Asset.Quantity - used, remaining);
                    remaining = remaining - assertPart;
                    used += assertPart;
                    if(used == previousAssetQueue.Peek().Asset.Quantity)
                    {
                        previousAssetQueue.Dequeue();
                        used = 0;
                    }
                }

                this.Transfers.Add(entry);
            }
        }

        private ColorMarker _Marker;
        public ColorMarker Marker
        {
            get
            {
                return this._Marker;
            }
            set
            {
                this._Marker = value;
            }
        }

        private List<ColoredEntry> _Issuances;
        public List<ColoredEntry> Issuances
        {
            get
            {
                return this._Issuances;
            }
            set
            {
                this._Issuances = value;
            }
        }

        private List<ColoredEntry> _Transfers;
        public List<ColoredEntry> Transfers
        {
            get
            {
                return this._Transfers;
            }
            set
            {
                this._Transfers = value;
            }
        }

        public AssetMoney[] GetDestroyedAssets()
        {
            IEnumerable<AssetMoney> burned = this.Inputs
                .Select(i => i.Asset)
                .GroupBy(i => i.Id)
                .Select(g => g.Sum(g.Key));

            IEnumerable<AssetMoney> transfered = this.Transfers
                .Select(i => i.Asset)
                .GroupBy(i => i.Id)
                .Select(g => -g.Sum(g.Key));

            return burned.Concat(transfered)
                .GroupBy(o => o.Id)
                .Select(g => g.Sum(g.Key))
                .Where(a => a.Quantity != 0)
                .ToArray();
        }

#region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            if(stream.Serializing)
            {
                if(this._Marker != null)
                    stream.ReadWrite(ref this._Marker);
                else
                    stream.ReadWrite(new Script());
            }
            else
            {
                var script = new Script();
                stream.ReadWrite(ref script);
                if(script.Length != 0)
                {
                    this._Marker = new ColorMarker(script);
                }
                else
                {
                }
            }
            stream.ReadWrite(ref this._Inputs);
            stream.ReadWrite(ref this._Issuances);
            stream.ReadWrite(ref this._Transfers);
        }

#endregion

        private List<ColoredEntry> _Inputs;
        public List<ColoredEntry> Inputs
        {
            get
            {
                return this._Inputs;
            }
            set
            {
                this._Inputs = value;
            }
        }
<<<<<<< HEAD
#if !NOJSONNET
=======

        public override string ToString()
        {
            return ToString(Network.Main);
        }

>>>>>>> 181ab516232e67199e7700968ccaec328eeb34e6
        public string ToString(Network network)
        {
            var obj = new JObject();
            var inputs = new JArray();
            obj.Add(new JProperty("inputs", inputs));
            foreach(ColoredEntry input in this.Inputs)
            {
                WriteEntry(network, inputs, input);
            }

            var issuances = new JArray();
            obj.Add(new JProperty("issuances", issuances));
            foreach(ColoredEntry issuance in this.Issuances)
            {
                WriteEntry(network, issuances, issuance);
            }

            var transfers = new JArray();
            obj.Add(new JProperty("transfers", transfers));
            foreach(ColoredEntry transfer in this.Transfers)
            {
                WriteEntry(network, transfers, transfer);
            }

            var destructions = new JArray();
            obj.Add(new JProperty("destructions", destructions));
            foreach(AssetMoney destuction in GetDestroyedAssets())
            {
                var asset = new JProperty("asset", destuction.Id.GetWif(network).ToString());
                var quantity = new JProperty("quantity", destuction.Quantity);
                inputs.Add(new JObject(asset, quantity));
            }

            return obj.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static void WriteEntry(Network network, JArray inputs, ColoredEntry entry)
        {
            var index = new JProperty("index", entry.Index);
            var asset = new JProperty("asset", entry.Asset.Id.GetWif(network).ToString());
            var quantity = new JProperty("quantity", entry.Asset.Quantity);
            inputs.Add(new JObject(index, asset, quantity));
        }

        //00000000000000001c7a19e8ef62d815d84a473f543de77f23b8342fc26812a9 at 299220 Monday, May 5, 2014 3:47:37 PM first block
        public static readonly DateTimeOffset FirstColoredDate = new DateTimeOffset(2014, 05, 4, 0, 0, 0, TimeSpan.Zero);
    }
}
