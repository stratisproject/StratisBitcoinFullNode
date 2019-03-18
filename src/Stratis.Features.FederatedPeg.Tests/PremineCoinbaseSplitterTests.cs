using System;
using NBitcoin;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class PremineCoinbaseSplitterTests
    {
        private readonly PremineCoinbaseSplitter premineSplitter;
        private readonly Network network;

        public PremineCoinbaseSplitterTests()
        {
            this.premineSplitter = new PremineCoinbaseSplitter();
            this.network = FederatedPegNetwork.NetworksSelector.Mainnet();
        }

        [Fact]
        public void SplitsPremineIntoManyOutputs()
        {
            Script receiver = PayToScriptHashTemplate.Instance.GenerateScriptPubKey(new ScriptId());
            TxIn coinbaseInput = TxIn.CreateCoinbase((int)this.network.Consensus.PremineHeight);

            Transaction tx = this.network.CreateTransaction();

            tx.Inputs.Add(coinbaseInput);
            tx.Outputs.Add(new TxOut(this.network.Consensus.PremineReward, receiver));

            this.premineSplitter.SplitReward(tx);

            // Distributes outputs equally
            Assert.Equal(PremineCoinbaseSplitter.FederationWalletOutputs, tx.Outputs.Count);
            foreach (TxOut output in tx.Outputs)
            {
                Assert.Equal(receiver, output.ScriptPubKey);
                Assert.Equal(this.network.Consensus.PremineReward / PremineCoinbaseSplitter.FederationWalletOutputs, output.Value);
            }

            // Doesn't touch inputs
            Assert.Single(tx.Inputs);
            Assert.Equal(coinbaseInput, tx.Inputs[0]);
        }

        [Fact]
        public void OnlySplitValidAmounts()
        {
            // Can't split small amount
            Script receiver = PayToScriptHashTemplate.Instance.GenerateScriptPubKey(new ScriptId());
            TxIn coinbaseInput = TxIn.CreateCoinbase((int)this.network.Consensus.PremineHeight);
            Transaction tx = this.network.CreateTransaction();
            tx.Inputs.Add(coinbaseInput);
            tx.Outputs.Add(new TxOut(PremineCoinbaseSplitter.FederationWalletOutputs - 1, receiver));
            Assert.Throws<Exception>(() => this.premineSplitter.SplitReward(tx));

            // Can't split non-divisible amount
            tx = this.network.CreateTransaction();
            tx.Inputs.Add(coinbaseInput);
            tx.Outputs.Add(new TxOut(PremineCoinbaseSplitter.FederationWalletOutputs * 100 + 6, receiver));
            Assert.Throws<Exception>(() => this.premineSplitter.SplitReward(tx));
        }
    }
}
