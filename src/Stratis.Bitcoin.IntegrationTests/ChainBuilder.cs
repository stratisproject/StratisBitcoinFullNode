using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ChainBuilder
    {
        ConcurrentChain _Chain = new ConcurrentChain();
        Network _Network;
        public ChainBuilder(Network network)
        {
            Guard.NotNull(network, nameof(network));

            this._Network = network;
            this._Chain = new ConcurrentChain(this._Network);
            this.MinerKey = new Key();
            this.MinerScriptPubKey = this.MinerKey.PubKey.Hash.ScriptPubKey;
        }

        public ConcurrentChain Chain
        {
            get
            {
                return this._Chain;
            }
        }

        public Key MinerKey
        {
            get;
            private set;
        }

        public Script MinerScriptPubKey
        {
            get;
            private set;
        }

        public Transaction Spend(ICoin[] coins, Money amount)
        {
            TransactionBuilder builder = new TransactionBuilder();
            builder.AddCoins(coins);
            builder.AddKeys(this.MinerKey);
            builder.Send(this.MinerScriptPubKey, amount);
            builder.SendFees(Money.Coins(0.01m));
            builder.SetChange(this.MinerScriptPubKey);
            var tx = builder.BuildTransaction(true);
            return tx;
        }

        public ICoin[] GetSpendableCoins()
        {
            return this._Blocks
                .Select(b => b.Value)
                .SelectMany(b => b.Transactions.Select(t => new
                {
                    Tx = t,
                    Block = b
                }))
                .Where(b => !b.Tx.IsCoinBase || (this._Chain.Height + 1) - this._Chain.GetBlock(b.Block.GetHash()).Height >= 100)
                .Select(b => b.Tx)
                .SelectMany(b => b.Outputs.AsIndexedOutputs())
                .Where(o => o.TxOut.ScriptPubKey == this.MinerScriptPubKey)
                .Select(o => new Coin(o))
                .ToArray();
        }

        public void Mine(int blockCount)
        {
            List<Block> blocks = new List<Block>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            for (int i = 0; i < blockCount; i++)
            {
                uint nonce = 0;
                Block block = new Block();
                block.Header.HashPrevBlock = this._Chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(this._Network, this._Chain.Tip);
                block.Header.UpdateTime(now, this._Network, this._Chain.Tip);
                var coinbase = new Transaction();
                coinbase.AddInput(TxIn.CreateCoinbase(this._Chain.Height + 1));
                coinbase.AddOutput(new TxOut(this._Network.GetReward(this._Chain.Height + 1), this.MinerScriptPubKey));
                block.AddTransaction(coinbase);
                foreach (var tx in this._Transactions)
                {
                    block.AddTransaction(tx);
                }
                block.UpdateMerkleRoot();
                while (!block.CheckProofOfWork())
                    block.Header.Nonce = ++nonce;
                block.Header.CacheHashes();
                blocks.Add(block);
                this._Transactions.Clear();
                this._Chain.SetTip(block.Header);
            }

            foreach (var b in blocks)
            {
                this._Blocks.Add(b.GetHash(), b);
            }
        }

        internal Dictionary<uint256, Block> _Blocks = new Dictionary<uint256, Block>();
        private List<Transaction> _Transactions = new List<Transaction>();
        public void Broadcast(Transaction tx)
        {
            this._Transactions.Add(tx);
        }
    }
}