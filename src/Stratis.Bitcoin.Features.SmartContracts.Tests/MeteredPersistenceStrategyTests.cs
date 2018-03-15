using System;
using Moq;
using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class MeteredPersistenceStrategyTests
    {
        [Fact]
        public void SmartContracts_MeteredPersistenceStrategy_TestNullInjectedArgsThrow()
        {
            var sr = new Mock<IContractStateRepository>();

            Assert.Throws<ArgumentNullException>(() => new MeteredPersistenceStrategy(null, new GasMeter((Gas) 0)));
            Assert.Throws<ArgumentNullException>(() => new MeteredPersistenceStrategy(sr.Object, null));
        }

        [Fact]
        public void SmartContracts_MeteredPersistenceStrategy_TestGasConsumed()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;

            var sr = new Mock<IContractStateRepository>();

            sr.Setup(m => m.SetStorageValue(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>()));

            Gas availableGas = (Gas) 100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            MeteredPersistenceStrategy strategy = new MeteredPersistenceStrategy(
                sr.Object,
                gasMeter
                );

            strategy.StoreBytes(
                testAddress, 
                testKey,
                testValue);

            sr.Verify(s => s.SetStorageValue(testAddress, testKey, testValue));            

            // Test that gas is used
            Assert.True(gasMeter.ConsumedGas < availableGas);
        }
    }
}
