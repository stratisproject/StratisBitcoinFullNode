using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules
{
    /// <summary>
    /// Rule to check if the serialized size of the proven header is less than 1 000 512 bytes.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules.ProvenHeaderValidationConsensusRule" />
    public class ProvenHeaderSizeRule : ProvenHeaderValidationConsensusRule
    {
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            if (context.SkipValidation || !this.IsProvenHeaderActivated(context))
                return;

            if (!this.IsProvenHeader(context))
                return;

            var header = context.ValidationContext.ChainedHeaderToValidate.Header;

            using (var ms = new MemoryStream())
            {
                header.ReadWrite(new BitcoinStream(ms, true));
                byte[] bytes = ms.ToArray();

                // TODO: replace with maximum proven header size
                uint maxProvenHeaderSerializedSize = this.PosParent.Network.Consensus.Options.MaxBlockSerializedSize;
                
                if (bytes.Length > maxProvenHeaderSerializedSize)
                {
                    this.Logger.LogTrace("(-)[PROVEN_HEADER_INVALID_SIZE]");
                    ConsensusErrors.ProvenHeaderSize.Throw();
                }
            }
        }
    }
}
