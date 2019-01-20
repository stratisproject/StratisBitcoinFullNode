using System;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Tests.Common
{
    public class TransactionsHelper
    {
        private static Random random = new Random();

        public static Transaction BuildNewTransactionFromExistingTransaction(Transaction inputTransaction, int index = 0)
        {
            var transaction = new Transaction();
            var outPoint = new OutPoint(inputTransaction, index);
            transaction.Inputs.Add(new TxIn(outPoint));
            Money outValue = Money.Satoshis(inputTransaction.TotalOut.Satoshi / 4);
            outValue.Should().NotBe(Money.Zero, "just to have an actual out");
            Script outScript = (new Key()).ScriptPubKey;
            transaction.Outputs.Add(new TxOut(outValue, outScript));
            return transaction;
        }

        /// <summary>Creates invalid PoW block with coinbase transaction.</summary>
        public static Block CreateDummyBlockWithTransaction(Network network, ChainedHeader tip)
        {
            Block block = network.Consensus.ConsensusFactory.CreateBlock();
            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(tip.Height + 1));
            coinbase.AddOutput(new TxOut(new Money(100 * Money.COIN), new Key()));
            block.AddTransaction(coinbase);

            block.Header.Version = (int)ThresholdConditionCache.VersionbitsTopBits;

            block.Header.HashPrevBlock = tip.HashBlock;
            block.Header.UpdateTime(DateTimeProvider.Default.GetTimeOffset(), network, tip);
            block.Header.Bits = block.Header.GetWorkRequired(network, tip);
            block.Header.Nonce = (uint)random.Next();

            block.GetHash();

            // populate the size
            block.ToBytes(network.Consensus.ConsensusFactory);
            return block;
        }
    }
}