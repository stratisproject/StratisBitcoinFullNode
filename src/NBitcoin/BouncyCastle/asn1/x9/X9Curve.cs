using System;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.BouncyCastle.Math.EC;
using NBitcoin.BouncyCastle.Utilities;

namespace NBitcoin.BouncyCastle.Asn1.X9
{
    /**
     * ASN.1 def for Elliptic-Curve Curve structure. See
     * X9.62, for further details.
     */
    internal class X9Curve
        : Asn1Encodable
    {
        private readonly ECCurve curve;
        private readonly byte[] seed;
        private readonly DerObjectIdentifier fieldIdentifier;

        public X9Curve(
            ECCurve curve)
            : this(curve, null)
        {
        }

        public X9Curve(
            ECCurve curve,
            byte[] seed)
        {
            if(curve == null)
                throw new ArgumentNullException("curve");

            this.curve = curve;
            this.seed = Arrays.Clone(seed);

            if(ECAlgorithms.IsFpCurve(curve))
            {
                this.fieldIdentifier = X9ObjectIdentifiers.PrimeField;
            }
            else if(ECAlgorithms.IsF2mCurve(curve))
            {
                this.fieldIdentifier = X9ObjectIdentifiers.CharacteristicTwoField;
            }
            else
            {
                throw new ArgumentException("This type of ECCurve is not implemented");
            }
        }

        public X9Curve(
            X9FieldID fieldID,
            Asn1Sequence seq)
        {
            if(fieldID == null)
                throw new ArgumentNullException("fieldID");
            if(seq == null)
                throw new ArgumentNullException("seq");

            this.fieldIdentifier = fieldID.Identifier;

            if(this.fieldIdentifier.Equals(X9ObjectIdentifiers.PrimeField))
            {
                BigInteger q = ((DerInteger)fieldID.Parameters).Value;
                var x9A = new X9FieldElement(q, (Asn1OctetString)seq[0]);
                var x9B = new X9FieldElement(q, (Asn1OctetString)seq[1]);
                this.curve = new FpCurve(q, x9A.Value.ToBigInteger(), x9B.Value.ToBigInteger());
            }
            else
            {
                if(this.fieldIdentifier.Equals(X9ObjectIdentifiers.CharacteristicTwoField))
                {
                    // Characteristic two field
                    var parameters = (DerSequence)fieldID.Parameters;
                    int m = ((DerInteger)parameters[0]).Value.IntValue;
                    var representation
                        = (DerObjectIdentifier)parameters[1];

                    int k1 = 0;
                    int k2 = 0;
                    int k3 = 0;
                    if(representation.Equals(X9ObjectIdentifiers.TPBasis))
                    {
                        // Trinomial basis representation
                        k1 = ((DerInteger)parameters[2]).Value.IntValue;
                    }
                    else
                    {
                        // Pentanomial basis representation
                        var pentanomial = (DerSequence)parameters[2];
                        k1 = ((DerInteger)pentanomial[0]).Value.IntValue;
                        k2 = ((DerInteger)pentanomial[1]).Value.IntValue;
                        k3 = ((DerInteger)pentanomial[2]).Value.IntValue;
                    }
                    var x9A = new X9FieldElement(m, k1, k2, k3, (Asn1OctetString)seq[0]);
                    var x9B = new X9FieldElement(m, k1, k2, k3, (Asn1OctetString)seq[1]);
                    // TODO Is it possible to get the order (n) and cofactor(h) too?
                    this.curve = new F2mCurve(m, k1, k2, k3, x9A.Value.ToBigInteger(), x9B.Value.ToBigInteger());
                }
            }
        }

        public ECCurve Curve
        {
            get
            {
                return this.curve;
            }
        }

        public byte[] GetSeed()
        {
            return Arrays.Clone(this.seed);
        }

        /**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         *  Curve ::= Sequence {
         *      a               FieldElement,
         *      b               FieldElement,
         *      seed            BIT STRING      OPTIONAL
         *  }
         * </pre>
         */
        public override Asn1Object ToAsn1Object()
        {
            var v = new Asn1EncodableVector();

            if(this.fieldIdentifier.Equals(X9ObjectIdentifiers.PrimeField)
                || this.fieldIdentifier.Equals(X9ObjectIdentifiers.CharacteristicTwoField))
            {
                v.Add(new X9FieldElement(this.curve.A).ToAsn1Object());
                v.Add(new X9FieldElement(this.curve.B).ToAsn1Object());
            }

            return new DerSequence(v);
        }
    }
}
