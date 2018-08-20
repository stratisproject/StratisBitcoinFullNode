using System;

namespace NBitcoin
{
    public class BuriedDeploymentsArray
    {
        private readonly int[] heights;

        public BuriedDeploymentsArray()
        {
            this.heights = new int[Enum.GetValues(typeof(BuriedDeployments)).Length];
        }

        public int this[BuriedDeployments index]
        {
            get => this.heights[(int)index];
            set => this.heights[(int)index] = value;
        }
    }

    public enum BuriedDeployments
    {
        /// <summary>
        /// Height in coinbase.
        /// </summary>
        BIP34,

        /// <summary>
        /// Height in OP_CLTV.
        /// </summary>
        BIP65,

        /// <summary>
        /// Strict DER signature.
        /// </summary>
        BIP66
    }
}