using System;
using NBitcoin;
using Stratis.SmartContracts.State;
using Stratis.SmartContracts.State.AccountAbstractionLayer;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class VinSerializerTests
    {
        [Fact]
        public void VinSerialization()
        {
            StoredVin vin = new StoredVin
            {
                Hash = new uint256((uint)new Random().Next(100000)),
                Nvout = (uint)new Random().Next(100000),
                Value = (ulong)new Random().Next(100000),
                Alive = 123
            };

            byte[] serialized = Serializers.VinSerializer.Serialize(vin);
            StoredVin deserialized = Serializers.VinSerializer.Deserialize(serialized);
            Assert.Equal(vin.Hash, deserialized.Hash);
            Assert.Equal(vin.Nvout, deserialized.Nvout);
            Assert.Equal(vin.Value, deserialized.Value);
            Assert.Equal(vin.Alive, deserialized.Alive);
        }
    }
}