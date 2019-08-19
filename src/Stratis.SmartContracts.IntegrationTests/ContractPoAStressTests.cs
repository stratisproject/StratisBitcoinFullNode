using System.Collections.Generic;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ContractPoAStressTests : IClassFixture<PoAMockChainFixture>
    {
        private readonly IMockChain mockChain;

        public ContractPoAStressTests(PoAMockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
        }

        [Fact]
        public void MaximumCreateTransactionsInABlock()
        {
            const int txsToSend = 100;

            var node1 = this.mockChain.Nodes[0];
            var node2 = this.mockChain.Nodes[1];

            // Load us up with 100 utxos
            Result<WalletSendTransactionModel> fundingResult = node1.SendTransaction(node1.MinerAddress.ScriptPubKey, Money.Coins(100m), txsToSend);
            this.mockChain.MineBlocks(1);

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/Auction.cs");
            Assert.True(compilationResult.Success);

            for (int i = 0; i < txsToSend; i++)
            {
                var response = node1.SendCreateContractTransaction(compilationResult.Compilation, 0, outpoints:new List<OutpointRequest>
                {
                    new OutpointRequest
                    {
                        TransactionId = fundingResult.Value.TransactionId.ToString(),
                        Index = i
                    }
                });
            }

            this.mockChain.WaitAllMempoolCount(txsToSend);
            this.mockChain.MineBlocks(1);

            // Just over block gas limit.
            // TODO: Update with block gas limit changes
            const int expectedTxsInBlock = 85;
            var lastBlock = node1.GetLastBlock();
            Assert.Equal(expectedTxsInBlock, lastBlock.Transactions.Count);
        }
    }
}
