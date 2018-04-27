namespace Stratis.Bitcoin.IntegrationTests
{
    public class SmartContractMemoryPoolTests
    {
        [Fact]
        public void AddToMempool()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                Transaction tx = new Transaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.MinerSecret, false);

                stratisNodeSync.Broadcast(tx);

                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }
    }
}
