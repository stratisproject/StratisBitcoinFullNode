using System.Linq;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.Utilities
{
    public class UnspentOutputs : IBitcoinSerializable
    {
        public UnspentOutputs()
        {

        }
        public UnspentOutputs(uint height, Transaction tx)
        {
            Guard.NotNull(tx, nameof(tx));

            this._Outputs = tx.Outputs.ToArray();
            this._TransactionId = tx.GetHash();
            this._Height = height;
            this._Version = tx.Version;
            this._IsCoinbase = tx.IsCoinBase;
            this._IsCoinstake = tx.IsCoinStake;
            this._Time = tx.Time;
        }

        public UnspentOutputs(uint256 txId, Coins coins)
        {
            this._TransactionId = txId;
            this.SetCoins(coins);
        }

        private void SetCoins(Coins coins)
        {
            this._IsCoinbase = coins.CoinBase;
            this._IsCoinstake = coins.CoinStake;
            this._Time = coins.Time;
            this._Height = coins.Height;
            this._Version = coins.Version;
            this._Outputs = new TxOut[coins.Outputs.Count];
            for (uint i = 0; i < this._Outputs.Length; i++)
            {
                this._Outputs[i] = coins.TryGetOutput(i);
            }
        }

        public UnspentOutputs(UnspentOutputs unspent)
        {
            this._TransactionId = unspent.TransactionId;
            this._IsCoinbase = unspent.IsCoinbase;
            this._IsCoinstake = unspent.IsCoinstake;
            this._Time = unspent.Time;
            this._Height = unspent.Height;
            this._Version = unspent.Version;
            this._Outputs = unspent._Outputs.ToArray();
        }

        public TxOut[] _Outputs;


        private uint256 _TransactionId;
        public uint256 TransactionId
        {
            get
            {
                return this._TransactionId;
            }
        }


        private uint _Version;
        public uint Version
        {
            get
            {
                return this._Version;
            }
        }

        private bool _IsCoinbase;
        public bool IsCoinbase
        {
            get
            {
                return this._IsCoinbase;
            }
        }

        private bool _IsCoinstake;
        public bool IsCoinstake
        {
            get
            {
                return this._IsCoinstake;
            }
        }

        private uint _Time;
        public uint Time
        {
            get
            {
                return this._Time;
            }
        }

        private uint _Height;
        public uint Height
        {
            get
            {
                return this._Height;
            }
        }

        public bool IsPrunable
        {
            get
            {
                return this._Outputs.All(o => o == null ||
                                    (o.ScriptPubKey.Length > 0 && o.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN));
            }
        }

        public bool IsFull
        {
            get
            {
                return this._Outputs.All(o => o != null);
            }
        }

        public int UnspentCount
        {
            get
            {
                return this._Outputs.Count(o => o != null);
            }
        }

        public bool IsAvailable(uint outputIndex)
        {
            return this.TryGetOutput(outputIndex) != null;
        }

        public TxOut TryGetOutput(uint outputIndex)
        {
            if (outputIndex >= this._Outputs.Length)
                return null;
            return this._Outputs[outputIndex];
        }

        public bool Spend(uint outputIndex)
        {
            if (outputIndex >= this._Outputs.Length)
                return false;
            if (this._Outputs[outputIndex] == null)
                return false;
            this._Outputs[outputIndex] = null;
            return true;
        }

        public void Spend(UnspentOutputs c)
        {
            for (int i = 0; i < this._Outputs.Length; i++)
            {
                if (c._Outputs[i] == null)
                    this._Outputs[i] = null;
            }
        }

        static TxIn CoinbaseTxIn = TxIn.CreateCoinbase(0);
        static TxIn NonCoinbaseTxIn = new TxIn(new OutPoint(uint256.One, 0));
        public Coins ToCoins()
        {
            var coins = new Coins
            {
                CoinBase = this.IsCoinbase,
                Height = this.Height,
                Version = this.Version,
                CoinStake = this.IsCoinstake,
                Time = this.Time
            };
            foreach (var output in this._Outputs)
            {
                coins.Outputs.Add(output == null ? Coins.NullTxOut : output);
            }
            coins.ClearUnspendable();
            return coins;
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this._TransactionId);
            if (stream.Serializing)
            {
                var c = this.ToCoins();
                stream.ReadWrite(c);
            }
            else
            {
                Coins c = null;
                stream.ReadWrite(ref c);
                this.SetCoins(c);
            }
        }

        public UnspentOutputs Clone()
        {
            return new UnspentOutputs(this);
        }
    }
}