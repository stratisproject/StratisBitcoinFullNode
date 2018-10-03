using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoABlockHeaderValidator
    {
        public void Sign(Key key, PoABlockHeader header)
        {
            ECDSASignature signature = key.Sign(header.GetHash());

            if (!signature.IsLowS)
                signature = signature.MakeCanonical();

            header.FederationSignature = new BlockSignature { Signature = signature.ToDER() };
        }

        public bool VerifySignature(PubKey pubKey, PoABlockHeader header)
        {
            if (header.FederationSignature.IsEmpty())
                return false;


            if (!ECDSASignature.IsValidDER(header.FederationSignature.Signature))
                return false;

            ECDSASignature signature = ECDSASignature.FromDER(header.FederationSignature.Signature);

            if (!signature.IsLowS)
                return false;

            bool isValidSignature = pubKey.Verify(header.GetHash(), signature);

            return isValidSignature;
        }
    }
}
