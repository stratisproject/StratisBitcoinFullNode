using System;
using NBitcoin.BouncyCastle.Crypto.Parameters;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Macs
{
    /**
    * HMAC implementation based on RFC2104
    *
    * H(K XOR opad, H(K XOR ipad, text))
    */
    internal class HMac
        : IMac
    {
        private const byte IPAD = (byte)0x36;
        private const byte OPAD = (byte)0x5C;

        private readonly IDigest digest;
        private readonly int digestSize;
        private readonly int blockLength;
        private IMemoable ipadState;
        private IMemoable opadState;

        private readonly byte[] inputPad;
        private readonly byte[] outputBuf;

        public HMac(IDigest digest)
        {
            this.digest = digest;
            this.digestSize = digest.GetDigestSize();
            this.blockLength = digest.GetByteLength();
            this.inputPad = new byte[this.blockLength];
            this.outputBuf = new byte[this.blockLength + this.digestSize];
        }

        public virtual string AlgorithmName
        {
            get
            {
                return this.digest.AlgorithmName + "/HMAC";
            }
        }

        public virtual IDigest GetUnderlyingDigest()
        {
            return this.digest;
        }

        public virtual void Init(ICipherParameters parameters)
        {
            this.digest.Reset();

            byte[] key = ((KeyParameter)parameters).GetKey();
            int keyLength = key.Length;

            if(keyLength > this.blockLength)
            {
                this.digest.BlockUpdate(key, 0, keyLength);
                this.digest.DoFinal(this.inputPad, 0);

                keyLength = this.digestSize;
            }
            else
            {
                Array.Copy(key, 0, this.inputPad, 0, keyLength);
            }

            Array.Clear(this.inputPad, keyLength, this.blockLength - keyLength);
            Array.Copy(this.inputPad, 0, this.outputBuf, 0, this.blockLength);

            XorPad(this.inputPad, this.blockLength, IPAD);
            XorPad(this.outputBuf, this.blockLength, OPAD);

            if(this.digest is IMemoable)
            {
                this.opadState = ((IMemoable) this.digest).Copy();

                ((IDigest) this.opadState).BlockUpdate(this.outputBuf, 0, this.blockLength);
            }

            this.digest.BlockUpdate(this.inputPad, 0, this.inputPad.Length);

            if(this.digest is IMemoable)
            {
                this.ipadState = ((IMemoable) this.digest).Copy();
            }
        }

        public virtual int GetMacSize()
        {
            return this.digestSize;
        }

        public virtual void Update(byte input)
        {
            this.digest.Update(input);
        }

        public virtual void BlockUpdate(byte[] input, int inOff, int len)
        {
            this.digest.BlockUpdate(input, inOff, len);
        }

        public virtual int DoFinal(byte[] output, int outOff)
        {
            this.digest.DoFinal(this.outputBuf, this.blockLength);

            if(this.opadState != null)
            {
                ((IMemoable) this.digest).Reset(this.opadState);
                this.digest.BlockUpdate(this.outputBuf, this.blockLength, this.digest.GetDigestSize());
            }
            else
            {
                this.digest.BlockUpdate(this.outputBuf, 0, this.outputBuf.Length);
            }

            int len = this.digest.DoFinal(output, outOff);

            Array.Clear(this.outputBuf, this.blockLength, this.digestSize);

            if(this.ipadState != null)
            {
                ((IMemoable) this.digest).Reset(this.ipadState);
            }
            else
            {
                this.digest.BlockUpdate(this.inputPad, 0, this.inputPad.Length);
            }

            return len;
        }

        /**
        * Reset the mac generator.
        */
        public virtual void Reset()
        {
            // Reset underlying digest
            this.digest.Reset();

            // Initialise the digest
            this.digest.BlockUpdate(this.inputPad, 0, this.inputPad.Length);
        }

        private static void XorPad(byte[] pad, int len, byte n)
        {
            for(int i = 0; i < len; ++i)
            {
                pad[i] ^= n;
            }
        }
    }
}
