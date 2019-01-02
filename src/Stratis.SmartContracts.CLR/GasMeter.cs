using Stratis.SmartContracts.CLR.Exceptions;

namespace Stratis.SmartContracts.CLR
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
        /// <remarks>TODO Can we make it more obvious when program execution will be interrupted?</remarks>
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