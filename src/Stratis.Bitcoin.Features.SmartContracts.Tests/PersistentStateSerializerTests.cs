using NBitcoin;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class PersistentStateSerializerTests
    {
        private PersistentStateSerializer serializer;

        public PersistentStateSerializerTests()
        {
            this.serializer = new PersistentStateSerializer();
        }

        [Fact]
        public void PersistentState_CanSerializeAllTypes()
        {
            // Checking that these all work for now. 
            // TODO: Check that these actually are serialized in a performant way
            TestType<Address>(new Address(new uint160(123456).ToString()));
            TestType<bool>(true);
            TestType<int>((int)32);
            TestType<long>((long)6775492);
            TestType<uint>((uint)101);
            TestType<ulong>((ulong)1245);
            TestType<byte>(new byte());
            TestType<sbyte>(new sbyte());
            TestType<byte[]>(new byte[] { 127, 123 });
            TestType<char>('c');
            TestType<string>("Test String");
        }

        private void TestType<T>(T input)
        {
            byte[] testBytes = this.serializer.Serialize(input);
            T output = this.serializer.Deserialize<T>(testBytes);
            Assert.Equal(input, output);
        }
    }
}