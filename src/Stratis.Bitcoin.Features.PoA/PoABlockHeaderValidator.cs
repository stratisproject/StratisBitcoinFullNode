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
            uint256 headerHash = header.GetHash();
            ECDSASignature signature = key.Sign(headerHash);

            header.BlockSignature = new BlockSignature { Signature = signature.ToDER() };
        }

        public bool VerifySignature(PubKey pubKey, PoABlockHeader header)
        {
            if ((header.BlockSignature == null) || header.BlockSignature.IsEmpty())
            {
                this.logger.LogTrace("(-)[NO_SIGNATURE]");
                return false;
            }

            if (!ECDSASignature.IsValidDER(header.BlockSignature.Signature))
            {
                this.logger.LogTrace("(-)[INVALID_DER]");
                return false;
            }

            ECDSASignature signature = ECDSASignature.FromDER(header.BlockSignature.Signature);

            if (!signature.IsLowS)
            {
                this.logger.LogTrace("(-)[NOT_CANONICAL]");
                return false;
            }

            uint256 headerHash = header.GetHash();
            bool isValidSignature = pubKey.Verify(headerHash, signature);

            return isValidSignature;
        }
    }
}
