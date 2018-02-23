using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts.Exceptions;

namespace Stratis.SmartContracts.Backend
{
    public class GasMeter
    {
        public GasMeter(Gas availableGas)
        {
            this.AvailableGas = availableGas;
            this.GasLimit = availableGas;
        }

        public Gas GasLimit { get; }

        public Gas AvailableGas { get; private set; }

        public Gas ConsumedGas => (Gas)(this.GasLimit - this.AvailableGas);

        private bool IsEnoughGas(Gas requiredGas)
        {
            return this.AvailableGas >= requiredGas;
        }

        internal void Spend(Gas spend)
        {
            //@TODO Can we make it more obvious when program execution will be interrupted?
            if (!this.IsEnoughGas(spend))
            {
                this.AvailableGas = (Gas)0;
                throw new OutOfGasException("Went over gas limit of " + this.GasLimit);
            }

            this.AvailableGas -= spend;
        }
    }
}