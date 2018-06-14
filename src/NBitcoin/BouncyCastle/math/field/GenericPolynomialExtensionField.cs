using System;

namespace NBitcoin.BouncyCastle.Math.Field
{
    internal class GenericPolynomialExtensionField
        : IPolynomialExtensionField
    {
        protected readonly IFiniteField subfield;
        protected readonly IPolynomial minimalPolynomial;

        internal GenericPolynomialExtensionField(IFiniteField subfield, IPolynomial polynomial)
        {
            this.subfield = subfield;
            this.minimalPolynomial = polynomial;
        }

        public virtual BigInteger Characteristic
        {
            get
            {
                return this.subfield.Characteristic;
            }
        }

        public virtual int Dimension
        {
            get
            {
                return this.subfield.Dimension * this.minimalPolynomial.Degree;
            }
        }

        public virtual IFiniteField Subfield
        {
            get
            {
                return this.subfield;
            }
        }

        public virtual int Degree
        {
            get
            {
                return this.minimalPolynomial.Degree;
            }
        }

        public virtual IPolynomial MinimalPolynomial
        {
            get
            {
                return this.minimalPolynomial;
            }
        }

        public override bool Equals(object obj)
        {
            if(this == obj)
            {
                return true;
            }
            var other = obj as GenericPolynomialExtensionField;
            if(null == other)
            {
                return false;
            }
            return this.subfield.Equals(other.subfield) && this.minimalPolynomial.Equals(other.minimalPolynomial);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
