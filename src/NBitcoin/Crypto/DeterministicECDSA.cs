using System;
using System.Linq;
using NBitcoin.BouncyCastle.Crypto;
using NBitcoin.BouncyCastle.Crypto.Digests;
using NBitcoin.BouncyCastle.Crypto.Parameters;
using NBitcoin.BouncyCastle.Crypto.Signers;

namespace NBitcoin.Crypto
{    
    internal class DeterministicECDSA : ECDsaSigner
    {
        private byte[] _buffer = new byte[0];
        private readonly IDigest _digest;

        public DeterministicECDSA()
            : base(new HMacDsaKCalculator(new Sha256Digest()))

        {
            this._digest = new Sha256Digest();
        }
        public DeterministicECDSA(Func<IDigest> digest)
            : base(new HMacDsaKCalculator(digest()))
        {
            this._digest = digest();
        }


        public void setPrivateKey(ECPrivateKeyParameters ecKey)
        {
            Init(true, ecKey);
        }

        public void update(byte[] buf)
        {
            this._buffer = this._buffer.Concat(buf).ToArray();
        }

        public byte[] sign()
        {
            var hash = new byte[this._digest.GetDigestSize()];
            this._digest.BlockUpdate(this._buffer, 0, this._buffer.Length);
            this._digest.DoFinal(hash, 0);
            this._digest.Reset();
            return signHash(hash);
        }

        public byte[] signHash(byte[] hash)
        {
            return new ECDSASignature(GenerateSignature(hash)).ToDER();
        }
    }
}
