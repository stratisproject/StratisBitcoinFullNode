using System;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Consensus;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.IntegrationTests.Common.MockChain;
using Stratis.SmartContracts.Executor.Reflection;
using Stratis.SmartContracts.Executor.Reflection.Compilation;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.SmartContracts
{
    public class ContractCreationTests
    {
        private readonly Network network;
        private readonly IAddressGenerator addressGenerator;

        public ContractCreationTests()
        {
            this.network = new SmartContractsRegTest();
            this.addressGenerator = new AddressGenerator();
        }

        [Fact]
        public void Test_CatCreation()
        {
            using (MockChain chain = new MockChain(2))
            {
                MockChainNode sender = chain.Nodes[0];
                MockChainNode receiver = chain.Nodes[1];

                sender.MineBlocks(10);

                SmartContractCompilationResult compilationResult = SmartContractCompiler.CompileFile("SmartContracts/ContractCreation.cs");
                Assert.True(compilationResult.Success);

                // Create contract and ensure code exists
                BuildCreateContractTransactionResponse response = sender.SendCreateContractTransaction(compilationResult.Compilation);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(2);
                Assert.NotNull(receiver.GetCode(response.NewContractAddress));
                Assert.NotNull(sender.GetCode(response.NewContractAddress));

                // Call contract and ensure internal contract was created.
                BuildCallContractTransactionResponse callResponse = sender.SendCallContractTransaction("CreateCat", response.NewContractAddress, 0);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);
                Assert.Equal(1, BitConverter.ToInt32(sender.GetStorageValue(response.NewContractAddress, "CatCounter")));
                uint160 lastCreatedCatAddress =  new uint160(sender.GetStorageValue(response.NewContractAddress, "LastCreatedCat"));
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
                BuildCallContractTransactionResponse callResponse2 = sender.SendCallContractTransaction("CreateCatWithFunds", response.NewContractAddress, 99);
                receiver.WaitMempoolCount(1);
                receiver.MineBlocks(1);

                // Check created contract has expected balance.
                lastCreatedCatAddress = new uint160(sender.GetStorageValue(response.NewContractAddress, "LastCreatedCat"));
                // TODO:

                // Check block has 3 transactions. Coinbase, our tx, and then a condensing tx.
                var block = receiver.GetLastBlock();
                Assert.Equal(3, block.Transactions.Count);
            }
        }
    }
}
