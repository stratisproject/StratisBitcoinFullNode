using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.CLR.Metering;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class GasMeterTests
    {
        [Fact]
        public void SmartContracts_GasMeter_NewHasCorrectInitialGas()
        {
            var gas = new RuntimeObserver.Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(gas, gasMeter.GasLimit);
        }

        [Fact]
        public void SmartContracts_GasMeter_NewHasAllAvailableGas()
        {
            var gas = new RuntimeObserver.Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(gas, gasMeter.GasAvailable);
        }

        [Fact]
        public void SmartContracts_GasMeter_NewHasNoConsumedGas()
        {
            var gas = new RuntimeObserver.Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(RuntimeObserver.Gas.None, gasMeter.GasConsumed);
        }

        [Fact]
        public void SmartContracts_GasMeter_HasEnoughGasOperation()
        {
            var diff = (RuntimeObserver.Gas)100;
            var gas = new RuntimeObserver.Gas(1000);
            var consumed = (RuntimeObserver.Gas)(gas - diff);
            var gasMeter = new GasMeter(gas);

            gasMeter.Spend(consumed);

            Assert.Equal(consumed, gasMeter.GasConsumed);
            Assert.Equal(diff, gasMeter.GasAvailable);
        }

        [Fact]
        public void SmartContract_GasMeter_DoesNotHaveEnoughGasOperation()
        {
            var gas = new RuntimeObserver.Gas(1000);
            var operationCost = new RuntimeObserver.Gas(1500);
            var gasMeter = new GasMeter(gas);

            Assert.Throws<OutOfGasException>(() => gasMeter.Spend(operationCost));
        }
    }
}
