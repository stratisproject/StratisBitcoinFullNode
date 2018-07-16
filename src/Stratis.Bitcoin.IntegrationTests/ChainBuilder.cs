using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ChainBuilder
    {
        private Network network;

        public ChainBuilder(Network network)
        {
            Guard.NotNull(network, nameof(network));

            this.network = network;
            this.Chain = new ConcurrentChain(this.network);
            this.MinerKey = new Key();
            this.MinerScriptPubKey = this.MinerKey.PubKey.Hash.ScriptPubKey;
        }

        public ConcurrentChain Chain { get; private set; }

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
            var builder = new TransactionBuilder(this.network);
            builder.AddCoins(coins);
            builder.AddKeys(this.MinerKey);
            builder.Send(this.MinerScriptPubKey, amount);
            builder.SendFees(Money.Coins(0.01m));
            builder.SetChange(this.MinerScriptPubKey);
            Transaction tx = builder.BuildTransaction(true);
            return tx;
        }

        public ICoin[] GetSpendableCoins()
        {
            return this.Blocks
                .Select(b => b.Value)
                .SelectMany(b => b.Transactions.Select(t => new
                {
                    Tx = t,
                    Block = b
                }))
                .Where(b => !b.Tx.IsCoinBase || (this.Chain.Height + 1) - this.Chain.GetBlock(b.Block.GetHash()).Height >= 100)
                .Select(b => b.Tx)
                .SelectMany(b => b.Outputs.AsIndexedOutputs())
                .Where(o => o.TxOut.ScriptPubKey == this.MinerScriptPubKey)
                .Select(o => new Coin(o))
                .ToArray();
        }

        public void Mine(int blockCount)
        {
            var blocks = new List<Block>();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            for (int i = 0; i < blockCount; i++)
            {
                uint nonce = 0;
                var block = this.network.CreateBlock();
                block.Header.HashPrevBlock = this.Chain.Tip.HashBlock;
                block.Header.Bits = block.Header.GetWorkRequired(this.network, this.Chain.Tip);
                block.Header.UpdateTime(now, this.network, this.Chain.Tip);

                var coinbase = this.network.CreateTransaction();
                coinbase.AddInput(TxIn.CreateCoinbase(this.Chain.Height + 1));
                coinbase.AddOutput(new TxOut(this.network.GetReward(this.Chain.Height + 1), this.MinerScriptPubKey));
                block.AddTransaction(coinbase);
                foreach (Transaction tx in this.transactions)
                {
                    block.AddTransaction(tx);
                }
                block.UpdateMerkleRoot();
                while (!block.CheckProofOfWork())
                    block.Header.Nonce = ++nonce;
                block.Header.PrecomputeHash();
                blocks.Add(block);
                this.transactions.Clear();
                this.Chain.SetTip(block.Header);
            }

            foreach (Block b in blocks)
            {
                this.Blocks.Add(b.GetHash(), b);
            }
        }

        internal Dictionary<uint256, Block> Blocks = new Dictionary<uint256, Block>();
        private List<Transaction> transactions = new List<Transaction>();

        public void Broadcast(Transaction tx)
        {
            this.transactions.Add(tx);
        }
    }
}
