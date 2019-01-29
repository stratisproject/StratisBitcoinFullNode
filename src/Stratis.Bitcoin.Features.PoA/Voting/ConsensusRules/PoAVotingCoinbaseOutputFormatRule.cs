using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.Voting.ConsensusRules
{
    /// <summary>Validates <see cref="VotingData"/> collection format if voting output is present in the coinbase transaction.</summary>
    public class PoAVotingCoinbaseOutputFormatRule : PartialValidationConsensusRule
    {
        private VotingDataEncoder votingDataEncoder;

        public override void Initialize()
        {
            this.votingDataEncoder = new VotingDataEncoder(this.Parent.LoggerFactory);

            base.Initialize();
        }

        public override Task RunAsync(RuleContext context)
        {
            Transaction coinbase = context.ValidationContext.BlockToValidate.Transactions[0];

            byte[] votingDataBytes = this.votingDataEncoder.ExtractRawVotingData(coinbase);

            if (votingDataBytes == null)
            {
                this.Logger.LogTrace("(-)[NO_VOTING_DATA]");
                return Task.CompletedTask;
            }

            List<VotingData> votingDataList = this.votingDataEncoder.Decode(votingDataBytes);

            // TODO verify format of voting data

            // find voting output. exit if not found. if found check that it's not the first output, then check the format
            // there could be only 1 voting output

            // max op return data is ushort.maxValue

            // TODO

            throw new NotImplementedException();
        }
    }
}
