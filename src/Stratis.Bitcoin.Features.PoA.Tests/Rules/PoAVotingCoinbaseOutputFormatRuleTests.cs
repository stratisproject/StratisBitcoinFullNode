using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.PoA.Voting.ConsensusRules;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoAVotingCoinbaseOutputFormatRuleTests : PoATestsBase
    {
        private readonly PoAVotingCoinbaseOutputFormatRule votingFormatRule;

        public PoAVotingCoinbaseOutputFormatRuleTests()
        {
            this.votingFormatRule = new PoAVotingCoinbaseOutputFormatRule();
            this.InitRule(this.votingFormatRule);
        }

        [Fact]
        public void DoesNothingIfThereIsNoVotingData()
        {
            Block block = new Block();
            block.Transactions.Add(new Transaction());
            block.Transactions[0].AddOutput(Money.COIN, Script.Empty);

            this.votingFormatRule.RunAsync(new RuleContext(new ValidationContext() {BlockToValidate = block}, DateTimeOffset.Now)).GetAwaiter().GetResult();
        }

        [Fact]
        public void ThrowsIfCantEncode()
        {
            List<byte> votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);
            votingData.AddRange(new List<byte>() { 1, 2, 3, 4, 5, 6, 7, 8});

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            var block = new Block();
            block.Transactions.Add(new Transaction());
            block.Transactions[0].AddOutput(Money.COIN, votingOutputScript);

            Assert.Throws<ConsensusErrorException>(() =>
                this.votingFormatRule.RunAsync(new RuleContext(new ValidationContext() { BlockToValidate = block }, DateTimeOffset.Now)).GetAwaiter().GetResult());
        }

        [Fact]
        public void ThrowsIfEmptyList()
        {
            var encoder = new VotingDataEncoder(new ExtendedLoggerFactory());
            byte[] bytes = encoder.Encode(new List<VotingData>());

            List<byte> votingData = new List<byte>(VotingDataEncoder.VotingOutputPrefixBytes);
            votingData.AddRange(bytes);

            var votingOutputScript = new Script(OpcodeType.OP_RETURN, Op.GetPushOp(votingData.ToArray()));

            var block = new Block();
            block.Transactions.Add(new Transaction());
            block.Transactions[0].AddOutput(Money.COIN, votingOutputScript);

            Assert.Throws<ConsensusErrorException>(() =>
                this.votingFormatRule.RunAsync(new RuleContext(new ValidationContext() {BlockToValidate = block}, DateTimeOffset.Now)).GetAwaiter().GetResult());
        }
    }
}
