using System;
using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class ContractUnspentOutputSerializerTests
    {
        [Fact]
        public void VinSerialization()
        {
            ContractUnspentOutput vin = new ContractUnspentOutput
            {
                Hash = new uint256((uint)new Random().Next(100000)),
                Nvout = (uint)new Random().Next(100000),
                Value = (ulong)new Random().Next(100000)
            };

            byte[] serialized = Serializers.ContractOutputSerializer.Serialize(vin);
            ContractUnspentOutput deserialized = Serializers.ContractOutputSerializer.Deserialize(serialized);
            Assert.Equal(vin.Hash, deserialized.Hash);
            Assert.Equal(vin.Nvout, deserialized.Nvout);
            Assert.Equal(vin.Value, deserialized.Value);
        }
    }
}