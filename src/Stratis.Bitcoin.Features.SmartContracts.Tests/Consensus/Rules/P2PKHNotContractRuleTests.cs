using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class P2PKHNotContractRuleTests
    {
        private readonly Network network;

        public P2PKHNotContractRuleTests()
        {
            this.network = new SmartContractsRegTest();
        }

        [Fact]
        public void SendTo_NotAContract_Success()
        {
            uint160 walletAddress = new uint160(321);

            var state = new Mock<IStateRepositoryRoot>();
            state.Setup(x => x.GetAccountState(walletAddress)).Returns<AccountState>(null);

            var rule = new P2PKHNotContractRule(state.Object);

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(walletAddress))));
            rule.CheckTransaction(new MempoolValidationContext(transaction, null));
        }

        [Fact]
        public void SendTo_Contract_Fails()
        {
            uint160 contractAddress = new uint160(123);

            var state = new Mock<IStateRepositoryRoot>();
            state.Setup(x => x.GetAccountState(contractAddress)).Returns(new AccountState()); // not null

            var rule = new P2PKHNotContractRule(state.Object);

            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(100, PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(contractAddress))));
            Assert.Throws<ConsensusErrorException>(() => rule.CheckTransaction(new MempoolValidationContext(transaction, null)));
        }
    }
}
