using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules
{
    /// <summary>
    /// Estimates which public key should be used for timestamp of a header being
    /// validated and uses this public key to verify header's signature.
    /// </summary>
    public class PoAHeaderSignatureRule : HeaderValidationConsensusRule
    {
        private PoABlockHeaderValidator validator;

        private SlotsManager slotsManager;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            var engine = this.Parent as PoAConsensusRuleEngine;

            this.slotsManager = engine.SlotsManager;
            this.validator = engine.poaHeaderValidator;
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
