using System;
using System.IO;
using NBitcoin.BouncyCastle.Asn1;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin.Crypto
{
    public class ECDSASignature
    {
        private readonly BigInteger _R;
        public BigInteger R
        {
            get
            {
                return this._R;
            }
        }
        private BigInteger _S;
        public BigInteger S
        {
            get
            {
                return this._S;
            }
        }
        public ECDSASignature(BigInteger r, BigInteger s)
        {
            this._R = r;
            this._S = s;
        }

        public ECDSASignature(BigInteger[] rs)
        {
            this._R = rs[0];
            this._S = rs[1];
        }

        public ECDSASignature(byte[] derSig)
        {
            try
            {
                var decoder = new Asn1InputStream(derSig);
                var seq = decoder.ReadObject() as DerSequence;
                if(seq == null || seq.Count != 2)
                    throw new FormatException(InvalidDERSignature);
                this._R = ((DerInteger)seq[0]).Value;
                this._S = ((DerInteger)seq[1]).Value;
            }
            catch(Exception ex)
            {
                throw new FormatException(InvalidDERSignature, ex);
            }
        }

        /**
        * What we get back from the signer are the two components of a signature, r and s. To get a flat byte stream
        * of the type used by Bitcoin we have to encode them using DER encoding, which is just a way to pack the two
        * components into a structure.
        */
        public byte[] ToDER()
        {
            // Usually 70-72 bytes.
            var bos = new MemoryStream(72);
            var seq = new DerSequenceGenerator(bos);
            seq.AddObject(new DerInteger(this.R));
            seq.AddObject(new DerInteger(this.S));
            seq.Close();
            return bos.ToArray();

        }

        private const string InvalidDERSignature = "Invalid DER signature";
        public static ECDSASignature FromDER(byte[] sig)
        {
            return new ECDSASignature(sig);
        }

        /// <summary>
        /// Enforce LowS on the signature
        /// </summary>
        public ECDSASignature MakeCanonical()
        {
            if(!this.IsLowS)
            {
                return new ECDSASignature(this.R, ECKey.CURVE_ORDER.Subtract(this.S));
            }
            else
                return this;
        }

        /// <summary>
        /// Allow creation of signature with non-LowS for test purposes
        /// </summary>
        /// <remarks>Not to be used under normal circumstances</remarks>
        public ECDSASignature MakeNonCanonical()
        {
            if (!this.IsLowS)
            {
                return this;
            }
            else
                return new ECDSASignature(this.R, ECKey.CURVE_ORDER.Subtract(this.S));
        }

        public bool IsLowS
        {
            get
            {
                return this.S.CompareTo(ECKey.HALF_CURVE_ORDER) <= 0;
            }
        }



        public static bool IsValidDER(byte[] bytes)
        {
            try
            {
                FromDER(bytes);
                return true;
            }
            catch(FormatException)
            {
                return false;
            }
            catch(Exception ex)
            {
                Utils.error("Unexpected exception in ECDSASignature.IsValidDER " + ex.Message);
                return false;
            }
        }
    }
}
