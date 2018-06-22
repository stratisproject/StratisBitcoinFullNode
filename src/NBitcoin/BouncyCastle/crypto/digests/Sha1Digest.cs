using System;
using NBitcoin.BouncyCastle.Crypto.Utilities;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Digests
{

    /**
     * implementation of SHA-1 as outlined in "Handbook of Applied Cryptography", pages 346 - 349.
     *
     * It is interesting to ponder why the, apart from the extra IV, the other difference here from MD5
     * is the "endianness" of the word processing!
     */
    internal class Sha1Digest
        : GeneralDigest
    {
        private const int DigestLength = 20;

        private uint H1, H2, H3, H4, H5;

        private uint[] X = new uint[80];
        private int xOff;

        public Sha1Digest()
        {
            Reset();
        }

        /**
         * Copy constructor.  This will copy the state of the provided
         * message digest.
         */
        public Sha1Digest(Sha1Digest t)
            : base(t)
        {
            CopyIn(t);
        }

        private void CopyIn(Sha1Digest t)
        {
            base.CopyIn(t);

            this.H1 = t.H1;
            this.H2 = t.H2;
            this.H3 = t.H3;
            this.H4 = t.H4;
            this.H5 = t.H5;

            Array.Copy(t.X, 0, this.X, 0, t.X.Length);
            this.xOff = t.xOff;
        }

        public override string AlgorithmName
        {
            get
            {
                return "SHA-1";
            }
        }

        public override int GetDigestSize()
        {
            return DigestLength;
        }

        internal override void ProcessWord(
            byte[] input,
            int inOff)
        {
            this.X[this.xOff] = Pack.BE_To_UInt32(input, inOff);

            if(++this.xOff == 16)
            {
                ProcessBlock();
            }
        }

        internal override void ProcessLength(long bitLength)
        {
            if(this.xOff > 14)
            {
                ProcessBlock();
            }

            this.X[14] = (uint)((ulong)bitLength >> 32);
            this.X[15] = (uint)((ulong)bitLength);
        }

        public override int DoFinal(
            byte[] output,
            int outOff)
        {
            Finish();

            Pack.UInt32_To_BE(this.H1, output, outOff);
            Pack.UInt32_To_BE(this.H2, output, outOff + 4);
            Pack.UInt32_To_BE(this.H3, output, outOff + 8);
            Pack.UInt32_To_BE(this.H4, output, outOff + 12);
            Pack.UInt32_To_BE(this.H5, output, outOff + 16);

            Reset();

            return DigestLength;
        }

        /**
         * reset the chaining variables
         */
        public override void Reset()
        {
            base.Reset();

            this.H1 = 0x67452301;
            this.H2 = 0xefcdab89;
            this.H3 = 0x98badcfe;
            this.H4 = 0x10325476;
            this.H5 = 0xc3d2e1f0;

            this.xOff = 0;
            Array.Clear(this.X, 0, this.X.Length);
        }

        //
        // Additive constants
        //
        private const uint Y1 = 0x5a827999;
        private const uint Y2 = 0x6ed9eba1;
        private const uint Y3 = 0x8f1bbcdc;
        private const uint Y4 = 0xca62c1d6;

        private static uint F(uint u, uint v, uint w)
        {
            return (u & v) | (~u & w);
        }

        private static uint H(uint u, uint v, uint w)
        {
            return u ^ v ^ w;
        }

        private static uint G(uint u, uint v, uint w)
        {
            return (u & v) | (u & w) | (v & w);
        }

        internal override void ProcessBlock()
        {
            //
            // expand 16 word block into 80 word block.
            //
            for(int i = 16; i < 80; i++)
            {
                uint t = this.X[i - 3] ^ this.X[i - 8] ^ this.X[i - 14] ^ this.X[i - 16];
                this.X[i] = t << 1 | t >> 31;
            }

            //
            // set up working variables.
            //
            uint A = this.H1;
            uint B = this.H2;
            uint C = this.H3;
            uint D = this.H4;
            uint E = this.H5;

            //
            // round 1
            //
            int idx = 0;

            for(int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + F(B, C, D) + E + X[idx++] + Y1
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + F(B, C, D) + this.X[idx++] + Y1;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + F(A, B, C) + this.X[idx++] + Y1;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + F(E, A, B) + this.X[idx++] + Y1;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + F(D, E, A) + this.X[idx++] + Y1;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + F(C, D, E) + this.X[idx++] + Y1;
                C = C << 30 | (C >> 2);
            }

            //
            // round 2
            //
            for(int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + H(B, C, D) + E + X[idx++] + Y2
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + H(B, C, D) + this.X[idx++] + Y2;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + H(A, B, C) + this.X[idx++] + Y2;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + H(E, A, B) + this.X[idx++] + Y2;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + H(D, E, A) + this.X[idx++] + Y2;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + H(C, D, E) + this.X[idx++] + Y2;
                C = C << 30 | (C >> 2);
            }

            //
            // round 3
            //
            for(int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + G(B, C, D) + E + X[idx++] + Y3
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + G(B, C, D) + this.X[idx++] + Y3;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + G(A, B, C) + this.X[idx++] + Y3;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + G(E, A, B) + this.X[idx++] + Y3;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + G(D, E, A) + this.X[idx++] + Y3;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + G(C, D, E) + this.X[idx++] + Y3;
                C = C << 30 | (C >> 2);
            }

            //
            // round 4
            //
            for(int j = 0; j < 4; j++)
            {
                // E = rotateLeft(A, 5) + H(B, C, D) + E + X[idx++] + Y4
                // B = rotateLeft(B, 30)
                E += (A << 5 | (A >> 27)) + H(B, C, D) + this.X[idx++] + Y4;
                B = B << 30 | (B >> 2);

                D += (E << 5 | (E >> 27)) + H(A, B, C) + this.X[idx++] + Y4;
                A = A << 30 | (A >> 2);

                C += (D << 5 | (D >> 27)) + H(E, A, B) + this.X[idx++] + Y4;
                E = E << 30 | (E >> 2);

                B += (C << 5 | (C >> 27)) + H(D, E, A) + this.X[idx++] + Y4;
                D = D << 30 | (D >> 2);

                A += (B << 5 | (B >> 27)) + H(C, D, E) + this.X[idx++] + Y4;
                C = C << 30 | (C >> 2);
            }

            this.H1 += A;
            this.H2 += B;
            this.H3 += C;
            this.H4 += D;
            this.H5 += E;

            //
            // reset start of the buffer.
            //
            this.xOff = 0;
            Array.Clear(this.X, 0, 16);
        }

        public override IMemoable Copy()
        {
            return new Sha1Digest(this);
        }

        public override void Reset(IMemoable other)
        {
            var d = (Sha1Digest)other;

            CopyIn(d);
        }

    }
}
