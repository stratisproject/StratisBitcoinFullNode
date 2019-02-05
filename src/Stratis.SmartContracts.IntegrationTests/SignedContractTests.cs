using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.ContractSigning;
using Stratis.SmartContracts.Core.ContractSigning;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class SignedContractTests
    {
        [Fact]
        public void Create_Signed_Contract()
        {
            using (SignedPoAMockChain chain = new SignedPoAMockChain(2).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

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

                // Compile file
                byte[] toSend = new CSharpContractSigner(new ContractSigner()).PackageSignedCSharpFile(new SignedContractsPoARegTest().SigningContractPrivKey, "SmartContracts/StorageDemo.cs");
                
                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 30);
                node1.WaitMempoolCount(1);
                chain.MineBlocks(1);

                // Check the balance exists at contract location.
                Assert.Equal((ulong)30 * 100_000_000, node1.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        // TODO: Test cases:
        // - Sending just code. No signature / Not RLPed.
        // - Sending code with invalid signature.

        private void PayHalfPremine(IMockChain chain, MockChainNode from, MockChainNode to)
        {
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(from.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            from.WaitMempoolCount(1);
            chain.MineBlocks(1);
        }
    }
}
