using System.Linq;
using System.Text;
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

            this.Outputs = tx.Outputs.ToArray();
            this.transactionId = tx.GetHash();
            this.Height = height;
            this.Version = tx.Version;
            this.IsCoinbase = tx.IsCoinBase;
            this.IsCoinstake = tx.IsCoinStake;
            this.Time = tx.Time;
        }

        public UnspentOutputs(uint256 txId, Coins coins)
        {
            this.transactionId = txId;
            this.SetCoins(coins);
        }

        private void SetCoins(Coins coins)
        {
            this.IsCoinbase = coins.CoinBase;
            this.IsCoinstake = coins.CoinStake;
            this.Time = coins.Time;
            this.Height = coins.Height;
            this.Version = coins.Version;
            this.Outputs = new TxOut[coins.Outputs.Count];
            for (uint i = 0; i < this.Outputs.Length; i++)
            {
                this.Outputs[i] = coins.TryGetOutput(i);
            }
        }

        public UnspentOutputs(UnspentOutputs unspent)
        {
            this.transactionId = unspent.TransactionId;
            this.IsCoinbase = unspent.IsCoinbase;
            this.IsCoinstake = unspent.IsCoinstake;
            this.Time = unspent.Time;
            this.Height = unspent.Height;
            this.Version = unspent.Version;
            this.Outputs = unspent.Outputs.ToArray();
        }

        /// <summary>
        /// The outputs of a transaction.
        /// </summary>
        /// <remarks>
        /// The behaviour of this collection is as following:
        /// If a UTXO is spent it will be set to null, but the size of the collection will not change.
        /// If the last item in the collection is spent (and set to null) when storing to disk the size
        /// of the collection will change and be reduced by the number of last items that are null.
        /// </remarks>
        public TxOut[] Outputs;

        private uint256 transactionId;

        public uint256 TransactionId
        {
            get
            {
                return this.transactionId;
            }
        }

        public uint Version { get; private set; }

        public bool IsCoinbase { get; private set; }

        public bool IsCoinstake { get; private set; }

        public uint Time { get; private set; }

        public uint Height { get; private set; }

        public bool IsPrunable
        {
            get
            {
                return this.Outputs.All(o => o == null ||
                                    (o.ScriptPubKey.Length > 0 && o.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN));
            }
        }

        public bool IsFull
        {
            get
            {
                return this.Outputs.All(o => o != null);
            }
        }

        public int UnspentCount
        {
            get
            {
                return this.Outputs.Count(o => o != null);
            }
        }

        public bool IsAvailable(uint outputIndex)
        {
            return this.TryGetOutput(outputIndex) != null;
        }

        public TxOut TryGetOutput(uint outputIndex)
        {
            if (outputIndex >= this.Outputs.Length)
                return null;
            return this.Outputs[outputIndex];
        }

        public bool Spend(uint outputIndex)
        {
            if (outputIndex >= this.Outputs.Length)
                return false;
            if (this.Outputs[outputIndex] == null)
                return false;
            this.Outputs[outputIndex] = null;
            return true;
        }

        public void Spend(UnspentOutputs c)
        {
            for (int i = 0; i < this.Outputs.Length; i++)
            {
                if (c.Outputs[i] == null)
                    this.Outputs[i] = null;
            }
        }

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
            foreach (TxOut output in this.Outputs)
            {
                coins.Outputs.Add(output == null ? Coins.NullTxOut : output);
            }

            coins.ClearUnspendable();
            return coins;
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.transactionId);
            if (stream.Serializing)
            {
                Coins c = this.ToCoins();
                stream.ReadWrite(c);
            }
            else
            {
                Coins c = null;
                stream.ReadWrite(ref c);
                this.SetCoins(c);
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"{nameof(this.transactionId)}={this.transactionId}");

            builder.AppendLine($"{nameof(this.Height)}={this.Height}");
            builder.AppendLine($"{nameof(this.Version)}={this.Version}");
            builder.AppendLine($"{nameof(this.IsCoinbase)}={this.IsCoinbase}");
            builder.AppendLine($"{nameof(this.IsCoinstake)}={this.IsCoinstake}");
            builder.AppendLine($"{nameof(this.Time)}={this.Time}");
            builder.AppendLine($"{nameof(this.Outputs)}.{nameof(this.Outputs.Length)}={this.Outputs.Length}");

            foreach (TxOut output in this.Outputs.Take(5))
                builder.AppendLine(output == null ? "null" : output.ToString());

            if (this.Outputs.Length > 5)
            {
                // Only log out the first 5 outputs to avoid cluttering the logs.
                builder.AppendLine($"{this.Outputs.Length - 5} more outputs...");
            }

            return builder.ToString();
        }

        public UnspentOutputs Clone()
        {
            return new UnspentOutputs(this);
        }
    }
}
