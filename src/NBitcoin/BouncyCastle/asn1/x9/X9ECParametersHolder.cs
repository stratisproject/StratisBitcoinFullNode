namespace NBitcoin.BouncyCastle.Asn1.X9
{
    internal abstract class X9ECParametersHolder
    {
        private X9ECParameters parameters;

        public X9ECParameters Parameters
        {
            get
            {
                lock(this)
                {
                    if(this.parameters == null)
                    {
                        this.parameters = CreateParameters();
                    }

                    return this.parameters;
                }
            }
        }

        protected abstract X9ECParameters CreateParameters();
    }
}
