using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.ConsensusRules
{
    public class PoAHeaderSignatureRule : HeaderValidationConsensusRule
    {
        private PoABlockHeaderValidator validator;

        private SlotsManager slotsManager;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            var ruleEngine = this.Parent as PoAConsensusRuleEngine;

            this.slotsManager = ruleEngine.SlotsManager;
            this.validator = ruleEngine.poaHeaderValidator;
        }

        public override void Run(RuleContext context)
        {
            var header = context.ValidationContext.ChainedHeaderToValidate.Header as PoABlockHeader;

            PubKey pubKey = this.slotsManager.GetPubKeyForTimestamp(header.Time);

            if (!this.validator.VerifySignature(pubKey, header))
            {
                this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
                PoAConsensusErrors.InvalidHeaderSignature.Throw();
            }
        }
    }
}
