using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractSigning;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.ContractSigning;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class SignedContractTests
    {
        // TODO: Fixture to save time running tests.

        private readonly SignedContractsPoARegTest network;

        public SignedContractTests()
        {
            this.network = new SignedContractsPoARegTest();
        }


        [Retry]
        public void Create_Signed_Contract()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Compile file
                byte[] toSend = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(this.network.SigningContractPrivKey, "SmartContracts/StorageDemo.cs");

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 30);
                node1.WaitMempoolCount(1);
                chain.MineBlocks(1);

                // Check the balance exists at contract location.
                Assert.Equal((ulong)30 * 100_000_000, node1.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        [Retry]
        public void Create_NoSignature_Fails()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Compile file
                byte[] contractBytes = ContractCompiler.CompileFile("SmartContracts/Auction.cs").Compilation;

                // Try to send create but ensure it fails because code is in incorrect format.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(contractBytes, 30);
                Assert.False(sendResponse.Success);
            }
        }

        [Retry]
        public void Create_InvalidSignature_Fails()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Sign file with incorrect key.
                byte[] toSend = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(new Key(), "SmartContracts/StorageDemo.cs");

                // Try to send create but ensure it fails because code is signed by different key.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 30);
                Assert.False(sendResponse.Success);
            }
        }

        [Retry]
        public async Task Create_NoSignature_Mempool_Rejects()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Create a valid transaction.
                byte[] toSend = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(this.network.SigningContractPrivKey, "SmartContracts/StorageDemo.cs");
                var buildResult = node1.BuildCreateContractTransaction(toSend, 0);

                // Replace the SC output ScriptPubKey with an invalid one.
                Transaction tx = node1.CoreNode.FullNode.Network.CreateTransaction(buildResult.Hex);
                TxOut txOut = tx.TryGetSmartContractTxOut();
                byte[] contractBytes = ContractCompiler.CompileFile("SmartContracts/Auction.cs").Compilation;
                var serializer = new CallDataSerializer(new ContractPrimitiveSerializer(this.network));
                byte[] newScript = serializer.Serialize(new ContractTxData(1, SmartContractFormatLogic.GasLimitMaximum, (RuntimeObserver.Gas) SmartContractMempoolValidator.MinGasPrice, contractBytes));
                txOut.ScriptPubKey = new Script(newScript);

                var broadcasterManager = node1.CoreNode.FullNode.NodeService<IBroadcasterManager>();
                // Try and broadcast invalid tx.
                await broadcasterManager.BroadcastTransactionAsync(tx);

                // Give it enough time to reach if it was valid.
                Thread.Sleep(3000);

                // Nothing arrives.
                Assert.Empty(node1.CoreNode.CreateRPCClient().GetRawMempool());

                // If we were to send a valid one the mempool increases.
                buildResult = node1.BuildCreateContractTransaction(toSend, 0);
                tx = node1.CoreNode.FullNode.Network.CreateTransaction(buildResult.Hex);
                await broadcasterManager.BroadcastTransactionAsync(tx);
                node1.WaitMempoolCount(1);
            }
        }

        [Retry]
        public async Task Create_InvalidSignature_Mempool_Rejects()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Create a valid transaction.
                byte[] toSend = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(this.network.SigningContractPrivKey, "SmartContracts/StorageDemo.cs");
                var buildResult = node1.BuildCreateContractTransaction(toSend, 0);

                // Replace the SC output ScriptPubKey with an invalid one.
                Transaction tx = node1.CoreNode.FullNode.Network.CreateTransaction(buildResult.Hex);
                TxOut txOut = tx.TryGetSmartContractTxOut();
                byte[] incorrectlySignedBytes = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(new Key(), "SmartContracts/StorageDemo.cs");
                var serializer = new CallDataSerializer(new ContractPrimitiveSerializer(this.network));
                byte[] newScript = serializer.Serialize(new ContractTxData(1, SmartContractFormatLogic.GasLimitMaximum, (RuntimeObserver.Gas)SmartContractMempoolValidator.MinGasPrice, incorrectlySignedBytes));
                txOut.ScriptPubKey = new Script(newScript);

                var broadcasterManager = node1.CoreNode.FullNode.NodeService<IBroadcasterManager>();
                // Try and broadcast invalid tx.
                await broadcasterManager.BroadcastTransactionAsync(tx);

                // Give it enough time to reach if it was valid.
                Thread.Sleep(3000);

                // Nothing arrives.
                Assert.Empty(node1.CoreNode.CreateRPCClient().GetRawMempool());

                // If we were to send a valid one the mempool increases.
                buildResult = node1.BuildCreateContractTransaction(toSend, 0);
                tx = node1.CoreNode.FullNode.Network.CreateTransaction(buildResult.Hex);
                await broadcasterManager.BroadcastTransactionAsync(tx);
                node1.WaitMempoolCount(1);
            }
        }

        private void SetupNodes(IMockChain chain, MockChainNode node1, MockChainNode node2)
        {
            // Get premine
            chain.MineBlocks(10);

            // Send half to other from whoever received premine
            if ((long)node1.WalletSpendableBalance == node1.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi)
            {
                PayHalfPremine(chain, node1, node2);
            }
            else
            {
                PayHalfPremine(chain, node2, node1);
            }
        }

        private void PayHalfPremine(IMockChain chain, MockChainNode from, MockChainNode to)
        {
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(from.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            from.WaitMempoolCount(1);
            chain.MineBlocks(1);
        }
    }
}
