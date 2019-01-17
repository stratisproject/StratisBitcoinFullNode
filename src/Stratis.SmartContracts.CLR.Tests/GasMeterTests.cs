using Stratis.SmartContracts.CLR.Exceptions;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class GasMeterTests
    {
        [Fact]
        public void SmartContracts_GasMeter_NewHasCorrectInitialGas()
        {
            var gas = new Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(gas, gasMeter.Limit);
        }

        [Fact]
        public void SmartContracts_GasMeter_NewHasAllAvailableGas()
        {
            var gas = new Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(gas, gasMeter.Available);
        }

        [Fact]
        public void SmartContracts_GasMeter_NewHasNoConsumedGas()
        {
            var gas = new Gas(1000);
            var gasMeter = new GasMeter(gas);

            Assert.Equal(Gas.None, gasMeter.Consumed);
        }

        [Fact]
        public void SmartContracts_GasMeter_HasEnoughGasOperation()
        {
            var diff = (Gas)100;
            var gas = new Gas(1000);
            var consumed = (Gas)(gas - diff);
            var gasMeter = new GasMeter(gas);

            gasMeter.Spend(consumed);

            Assert.Equal(consumed, gasMeter.Consumed);
            Assert.Equal(diff, gasMeter.Available);
        }

        [Fact]
        public void SmartContract_GasMeter_DoesNotHaveEnoughGasOperation()
        {
            var gas = new Gas(1000);
            var operationCost = new Gas(1500);
            var gasMeter = new GasMeter(gas);

            Assert.Throws<OutOfGasException>(() => gasMeter.Spend(operationCost));
        }
    }
}
