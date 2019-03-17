using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Metering
{
    /// <inheritdoc/>
    public sealed class GasMeter : IGasMeter
    {
        /// <inheritdoc/>
        public Gas GasAvailable { get; private set; }

        /// <inheritdoc/>
        public Gas GasConsumed
        {
            get { return (Gas)(this.GasLimit - this.GasAvailable); }
        }

        /// <inheritdoc/>
        public Gas GasLimit { get; }

        public GasMeter(Gas gasAvailable)
        {
            this.GasAvailable = gasAvailable;
            this.GasLimit = gasAvailable;
        }

        /// <inheritdoc/>
        public void Spend(Gas gasToSpend)
        {
            if (this.GasAvailable >= gasToSpend)
            {
                this.GasAvailable -= gasToSpend;
                return;
            }

            this.GasAvailable = (Gas)0;

            throw new OutOfGasException("Went over gas limit of " + this.GasLimit);
        }
    }
}