using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public class SmartContractMemoryPoolTests
    {
        [Fact]
        public void SmartContracts_AddToMempool_Success()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                var stratisNodeSync = builder.CreateSmartContractPowNode().WithWallet().Start();

                TestHelper.MineBlocks(stratisNodeSync, 105); // coinbase maturity = 100

                var block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                var tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);

                stratisNodeSync.Broadcast(tx);

                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void SmartContracts_AddToMempool_OnlyValid()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                var stratisNodeSync = builder.CreateSmartContractPowNode().WithWallet().Start();

                var callDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(stratisNodeSync.FullNode.Network));

                TestHelper.MineBlocks(stratisNodeSync, 105); // coinbase maturity = 100

                var block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                // Gas higher than allowed limit
                var tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                var contractTxData = new ContractTxData(1, 100, new RuntimeObserver.Gas(10_000_000), new uint160(0), "Test");
                tx.AddOutput(new TxOut(1, new Script(callDataSerializer.Serialize(contractTxData))));
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(tx);

                // OP_SPEND in user's tx - we can't sign this because the TransactionBuilder recognises the ScriptPubKey is invalid.
                tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), new Script(new[] { (byte)ScOpcodeType.OP_SPEND })));
                tx.AddOutput(new TxOut(1, new Script(callDataSerializer.Serialize(contractTxData))));
                stratisNodeSync.Broadcast(tx);

                // 2 smart contract outputs
                tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut(1, new Script(callDataSerializer.Serialize(contractTxData))));
                tx.AddOutput(new TxOut(1, new Script(callDataSerializer.Serialize(contractTxData))));
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(tx);

                // Send to contract
                uint160 contractAddress = new uint160(123);
                var state = stratisNodeSync.FullNode.NodeService<IStateRepositoryRoot>();
                state.CreateAccount(contractAddress);
                state.Commit();
                tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(contractAddress))));
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(tx);

                // Gas price lower than minimum
                tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                var lowGasPriceContractTxData = new ContractTxData(1, SmartContractMempoolValidator.MinGasPrice - 1, new RuntimeObserver.Gas(SmartContractFormatLogic.GasLimitMaximum), new uint160(0), "Test");
                tx.AddOutput(new TxOut(1, new Script(callDataSerializer.Serialize(lowGasPriceContractTxData))));
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(tx);

                // After 5 seconds (plenty of time but ideally we would have a more accurate measure) no txs in mempool. All failed validation.
                Thread.Sleep(5000);
                Assert.Empty(stratisNodeSync.CreateRPCClient().GetRawMempool());

                // Valid tx still works
                tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(tx);
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }
    }
}