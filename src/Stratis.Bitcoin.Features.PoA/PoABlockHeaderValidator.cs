using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoABlockHeaderValidator
    {
        private readonly ILogger logger;

        public PoABlockHeaderValidator(ILoggerFactory factory)
        {
            this.logger = factory.CreateLogger(this.GetType().FullName);
        }

        public void Sign(Key key, PoABlockHeader header)
        {
            ECDSASignature signature = key.Sign(header.GetHash());

            header.FederationSignature = new BlockSignature { Signature = signature.ToDER() };
        }

        public bool VerifySignature(PubKey pubKey, PoABlockHeader header)
        {
            if (header.FederationSignature.IsEmpty())
            {
                this.logger.LogTrace("(-)[EMPTY_SIGNATURE]");
                return false;
            }

            if (!ECDSASignature.IsValidDER(header.FederationSignature.Signature))
            {
                this.logger.LogTrace("(-)[INVALID_DER]");
                return false;
            }

            ECDSASignature signature = ECDSASignature.FromDER(header.FederationSignature.Signature);

            if (!signature.IsLowS)
            {
                this.logger.LogTrace("(-)[NOT_CANONICAL]");
                return false;
            }

            bool isValidSignature = pubKey.Verify(header.GetHash(), signature);

            return isValidSignature;
        }
    }
}
