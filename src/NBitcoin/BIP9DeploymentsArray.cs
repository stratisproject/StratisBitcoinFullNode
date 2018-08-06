using System;

namespace NBitcoin
{
    public class BIP9DeploymentsArray
    {
        private readonly BIP9DeploymentsParameters[] parameters;

        public BIP9DeploymentsArray()
        {
            this.parameters = new BIP9DeploymentsParameters[Enum.GetValues(typeof(BIP9Deployments)).Length];
        }

        public BIP9DeploymentsParameters this[BIP9Deployments index]
        {
            get => this.parameters[(int)index];
            set => this.parameters[(int)index] = value;
        }
    }
}