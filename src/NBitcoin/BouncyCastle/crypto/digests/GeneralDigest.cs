using System;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Digests
{
    /**
    * base implementation of MD4 family style digest as outlined in
    * "Handbook of Applied Cryptography", pages 344 - 347.
    */
    internal abstract class GeneralDigest
        : IDigest, IMemoable
    {
        private const int BYTE_LENGTH = 64;

        private byte[] xBuf;
        private int xBufOff;

        private long byteCount;

        internal GeneralDigest()
        {
            this.xBuf = new byte[4];
        }

        internal GeneralDigest(GeneralDigest t)
        {
            this.xBuf = new byte[t.xBuf.Length];
            CopyIn(t);
        }

        protected void CopyIn(GeneralDigest t)
        {
            Array.Copy(t.xBuf, 0, this.xBuf, 0, t.xBuf.Length);

            this.xBufOff = t.xBufOff;
            this.byteCount = t.byteCount;
        }

        public void Update(byte input)
        {
            this.xBuf[this.xBufOff++] = input;

            if(this.xBufOff == this.xBuf.Length)
            {
                ProcessWord(this.xBuf, 0);
                this.xBufOff = 0;
            }

            this.byteCount++;
        }

        public void BlockUpdate(
            byte[] input,
            int inOff,
            int length)
        {
            length = System.Math.Max(0, length);

            //
            // fill the current word
            //
            int i = 0;
            if(this.xBufOff != 0)
            {
                while(i < length)
                {
                    this.xBuf[this.xBufOff++] = input[inOff + i++];
                    if(this.xBufOff == 4)
                    {
                        ProcessWord(this.xBuf, 0);
                        this.xBufOff = 0;
                        break;
                    }
                }
            }

            //
            // process whole words.
            //
            int limit = ((length - i) & ~3) + i;
            for(; i < limit; i += 4)
            {
                ProcessWord(input, inOff + i);
            }

            //
            // load in the remainder.
            //
            while(i < length)
            {
                this.xBuf[this.xBufOff++] = input[inOff + i++];
            }

            this.byteCount += length;
        }

        public void Finish()
        {
            long bitLength = (this.byteCount << 3);

            //
            // add the pad bytes.
            //
            Update((byte)128);

            while(this.xBufOff != 0)
                Update((byte)0);
            ProcessLength(bitLength);
            ProcessBlock();
        }

        public virtual void Reset()
        {
            this.byteCount = 0;
            this.xBufOff = 0;
            Array.Clear(this.xBuf, 0, this.xBuf.Length);
        }

        public int GetByteLength()
        {
            return BYTE_LENGTH;
        }

        internal abstract void ProcessWord(byte[] input, int inOff);
        internal abstract void ProcessLength(long bitLength);
        internal abstract void ProcessBlock();
        public abstract string AlgorithmName
        {
            get;
        }
        public abstract int GetDigestSize();
        public abstract int DoFinal(byte[] output, int outOff);
        public abstract IMemoable Copy();
        public abstract void Reset(IMemoable t);
    }
}
