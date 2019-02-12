using System;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoW
{
    public class ContractCreationTests
    {
        private readonly IAddressGenerator addressGenerator;

        public ContractCreationTests()
        {
            this.addressGenerator = new AddressGenerator();
        }

        [Fact]
        public void Test_CatCreation()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(1);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractCreation.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Call contract and ensure internal contract was created.
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("CreateCat", response.NewContractAddress, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);

                Assert.Equal(1, BitConverter.ToInt32(sender.GetStorageValue(response.NewContractAddress, "CatCounter")));
                uint160 lastCreatedCatAddress = new uint160(sender.GetStorageValue(response.NewContractAddress, "LastCreatedCat"));
                uint160 expectedCreatedCatAddress = this.addressGenerator.GenerateAddress(callResponse.TransactionId, 0);
                Assert.Equal(expectedCreatedCatAddress, lastCreatedCatAddress);

                // Test that the contract address, event name, and logging values are available in the bloom, from internal create.
                var scBlockHeader = receiver.GetLastBlock().Header as SmartContractBlockHeader;
                Assert.True(scBlockHeader.LogsBloom.Test(lastCreatedCatAddress.ToBytes()));
                Assert.True(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("CatCreated")));
                Assert.True(scBlockHeader.LogsBloom.Test(BitConverter.GetBytes(0)));
                // And sanity test that a random value is not available in bloom.
                Assert.False(scBlockHeader.LogsBloom.Test(Encoding.UTF8.GetBytes("RandomValue")));

                // Do a create that should transfer all funds sent now.
                decimal amount = 20;
                BuildCallContractTransactionResponse callResponse2 = sender.SendCallContractTransaction("CreateCatWithFunds", response.NewContractAddress, amount);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);

                // Check created contract has expected balance.
                lastCreatedCatAddress = new uint160(sender.GetStorageValue(response.NewContractAddress, "LastCreatedCat"));
                Assert.Equal(amount * Money.COIN, sender.GetContractBalance(lastCreatedCatAddress.ToBase58Address(sender.CoreNode.FullNode.Network)));

                // Check block has 3 transactions. Coinbase, our tx, and then a condensing tx.
                var block = receiver.GetLastBlock();
                Assert.Equal(3, block.Transactions.Count);
                // Condensing tx has 1 input and 1 output - FROM: real tx. TO: new contract address.
                Assert.Single(block.Transactions[2].Inputs);
                Assert.Single(block.Transactions[2].Outputs);
                Assert.Equal(block.Transactions[1].GetHash(), block.Transactions[2].Inputs[0].PrevOut.Hash); //  References tx above.
                Assert.Equal(amount * Money.COIN, (ulong)block.Transactions[2].Outputs[0].Value);
                Assert.True(block.Transactions[2].Inputs[0].ScriptSig.IsSmartContractSpend());
                Assert.True(block.Transactions[2].Outputs[0].ScriptPubKey.IsSmartContractInternalCall());
            }
        }

        [Fact]
        public void Test_IsContract_On_InternallyCreatedContract()
        {
            using (PoWMockChain chain = new PoWMockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(1);

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/ContractCreation.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Call contract and ensure internal contract was created.
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("CreateCatIsContract", response.NewContractAddress, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);

                Assert.Equal(1, BitConverter.ToInt32(sender.GetStorageValue(response.NewContractAddress, "CatCounter")));
                uint160 lastCreatedCatAddress = new uint160(sender.GetStorageValue(response.NewContractAddress, "LastCreatedCat"));
                uint160 expectedCreatedCatAddress = this.addressGenerator.GenerateAddress(callResponse.TransactionId, 0);
                Assert.Equal(expectedCreatedCatAddress, lastCreatedCatAddress);

                Assert.True(BitConverter.ToBoolean(sender.GetStorageValue(response.NewContractAddress, "IsContract")));
            }
        }
    }
}
