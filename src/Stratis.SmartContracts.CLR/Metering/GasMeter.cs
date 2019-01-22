using Stratis.SmartContracts.CLR.Exceptions;

namespace Stratis.SmartContracts.CLR.Metering
{
    /// <inheritdoc/>
    public sealed class GasMeter : RuntimeObserver.IGasMeter
    {
        /// <inheritdoc/>
        public RuntimeObserver.Gas GasAvailable { get; private set; }

        /// <inheritdoc/>
        public RuntimeObserver.Gas GasConsumed
        {
            get { return (RuntimeObserver.Gas)(this.GasLimit - this.GasAvailable); }
        }

        /// <inheritdoc/>
        public RuntimeObserver.Gas GasLimit { get; }

        public GasMeter(RuntimeObserver.Gas gasAvailable)
        {
            this.GasAvailable = gasAvailable;
            this.GasLimit = gasAvailable;
        }

        /// <inheritdoc/>
        public void Spend(RuntimeObserver.Gas gasToSpend)
        {
            if (this.GasAvailable >= gasToSpend)
            {
                this.GasAvailable -= gasToSpend;
                return;
            }

            this.GasAvailable = (RuntimeObserver.Gas)0;

            throw new OutOfGasException("Went over gas limit of " + this.GasLimit);
        }
    }
}