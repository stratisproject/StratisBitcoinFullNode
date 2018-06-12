using System;
using NBitcoin.BouncyCastle.Crypto.Macs;
using NBitcoin.BouncyCastle.Crypto.Parameters;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Security;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Crypto.Signers
{
    /**
     * A deterministic K calculator based on the algorithm in section 3.2 of RFC 6979.
     */
    internal class HMacDsaKCalculator
        : IDsaKCalculator
    {
        private readonly HMac hMac;
        private readonly byte[] K;
        private readonly byte[] V;

        private BigInteger n;

        /**
         * Base constructor.
         *
         * @param digest digest to build the HMAC on.
         */
        public HMacDsaKCalculator(IDigest digest)
        {
            this.hMac = new HMac(digest);
            this.V = new byte[this.hMac.GetMacSize()];
            this.K = new byte[this.hMac.GetMacSize()];
        }

        public virtual bool IsDeterministic
        {
            get
            {
                return true;
            }
        }

        public virtual void Init(BigInteger n, SecureRandom random)
        {
            throw new InvalidOperationException("Operation not supported");
        }

        public void Init(BigInteger n, BigInteger d, byte[] message)
        {
            this.n = n;

            Arrays.Fill(this.V, (byte)0x01);
            Arrays.Fill(this.K, (byte)0);

            var x = new byte[(n.BitLength + 7) / 8];
            byte[] dVal = BigIntegers.AsUnsignedByteArray(d);

            Array.Copy(dVal, 0, x, x.Length - dVal.Length, dVal.Length);

            var m = new byte[(n.BitLength + 7) / 8];

            BigInteger mInt = BitsToInt(message);

            if(mInt.CompareTo(n) >= 0)
            {
                mInt = mInt.Subtract(n);
            }

            byte[] mVal = BigIntegers.AsUnsignedByteArray(mInt);

            Array.Copy(mVal, 0, m, m.Length - mVal.Length, mVal.Length);

            this.hMac.Init(new KeyParameter(this.K));

            this.hMac.BlockUpdate(this.V, 0, this.V.Length);
            this.hMac.Update((byte)0x00);
            this.hMac.BlockUpdate(x, 0, x.Length);
            this.hMac.BlockUpdate(m, 0, m.Length);

            this.hMac.DoFinal(this.K, 0);

            this.hMac.Init(new KeyParameter(this.K));

            this.hMac.BlockUpdate(this.V, 0, this.V.Length);

            this.hMac.DoFinal(this.V, 0);

            this.hMac.BlockUpdate(this.V, 0, this.V.Length);
            this.hMac.Update((byte)0x01);
            this.hMac.BlockUpdate(x, 0, x.Length);
            this.hMac.BlockUpdate(m, 0, m.Length);

            this.hMac.DoFinal(this.K, 0);

            this.hMac.Init(new KeyParameter(this.K));

            this.hMac.BlockUpdate(this.V, 0, this.V.Length);

            this.hMac.DoFinal(this.V, 0);
        }

        public virtual BigInteger NextK()
        {
            var t = new byte[((this.n.BitLength + 7) / 8)];

            for(;;)
            {
                int tOff = 0;

                while(tOff < t.Length)
                {
                    this.hMac.BlockUpdate(this.V, 0, this.V.Length);

                    this.hMac.DoFinal(this.V, 0);

                    int len = System.Math.Min(t.Length - tOff, this.V.Length);
                    Array.Copy(this.V, 0, t, tOff, len);
                    tOff += len;
                }

                BigInteger k = BitsToInt(t);

                if(k.SignValue > 0 && k.CompareTo(this.n) < 0)
                {
                    return k;
                }

                this.hMac.BlockUpdate(this.V, 0, this.V.Length);
                this.hMac.Update((byte)0x00);

                this.hMac.DoFinal(this.K, 0);

                this.hMac.Init(new KeyParameter(this.K));

                this.hMac.BlockUpdate(this.V, 0, this.V.Length);

                this.hMac.DoFinal(this.V, 0);
            }
        }

        private BigInteger BitsToInt(byte[] t)
        {
            var v = new BigInteger(1, t);

            if(t.Length * 8 > this.n.BitLength)
            {
                v = v.ShiftRight(t.Length * 8 - this.n.BitLength);
            }

            return v;
        }
    }
}
