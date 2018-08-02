﻿using System.Linq;
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

        public UnspentOutputs Clone()
        {
            return new UnspentOutputs(this);
        }
    }
}
