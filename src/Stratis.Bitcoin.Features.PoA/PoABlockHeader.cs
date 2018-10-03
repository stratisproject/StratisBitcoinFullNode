using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoABlockHeader : BlockHeader
    {
        private BlockSignature federationSignature;

        public BlockSignature FederationSignature { get => this.federationSignature; }

        //ECDSASignature signature = coinstakeContext.Key.Sign(block.GetHash());
        //block.BlockSignature = new BlockSignature { Signature = signature.ToDER() };
        //BlockSignature

        public override void ReadWrite(BitcoinStream stream)
        {
            base.ReadWrite(stream);

            stream.ReadWrite(ref this.federationSignature);
        }

        // TODO move this code to PoA header validator

        public void Sign(Key key)
        {
            ECDSASignature signature = key.Sign(this.GetHash());

            if (!signature.IsLowS)
                signature = signature.MakeCanonical();

            this.federationSignature = new BlockSignature { Signature = signature.ToDER() };
        }

        public bool VerifySignature(PubKey pubKey)
        {
            if (!ECDSASignature.IsValidDER(this.FederationSignature.Signature))
                return false;

            ECDSASignature signature = ECDSASignature.FromDER(this.FederationSignature.Signature);

            if (!signature.IsLowS)
                return false;

            bool res = pubKey.Verify(this.GetHash(), signature);

            return res;
        }
    }
}
