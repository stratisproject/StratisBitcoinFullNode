using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts;
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
        public void Test1()
        {
            // Checking that these all work for now. TODO: Check that these actually are serialized in a performant way
            TestType<Address>(new Address(new uint160(123456)));
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

            //byte[] testAddressBytes = this.serializer.Serialize(testAddress);
            //Address testAddress2 = this.serializer.Deserialize<Address>(testAddressBytes);
            //Assert.Equal(testAddress, testAddress2);

            //bool testBool = true;
            //byte[] testBoolBytes = this.serializer.Serialize(testBool);
            //bool testBool2 = this.serializer.Deserialize<bool>(testBoolBytes);
            //Assert.Equal(testBool, testBool2);

            //bool testByte = true;
            //byte[] testByteBytes = this.serializer.Serialize(testByte);
            //bool testBool2 = this.serializer.Deserialize<bool>(testBoolBytes);
            //Assert.Equal(testBool, testBool2);

            ////Address testAddress = new Address(new uint160(123456));
            ////byte[] testAddressBytes = this.serializer.Serialize(testAddress);
            ////Address testAddress2 = this.serializer.Deserialize<Address>(testAddressBytes);
            ////Assert.Equal(testAddress, testAddress2);

            ////Address testAddress = new Address(new uint160(123456));
            ////byte[] testAddressBytes = this.serializer.Serialize(testAddress);
            ////Address testAddress2 = this.serializer.Deserialize<Address>(testAddressBytes);
            ////Assert.Equal(testAddress, testAddress2);

            ////Address testAddress = new Address(new uint160(123456));
            ////byte[] testAddressBytes = this.serializer.Serialize(testAddress);
            ////Address testAddress2 = this.serializer.Deserialize<Address>(testAddressBytes);
            ////Assert.Equal(testAddress, testAddress2);

        }

        private void TestType<T> (T input)
        {
            byte[] testBytes = this.serializer.Serialize(input);
            T output = this.serializer.Deserialize<T>(testBytes);
            Assert.Equal(input, output);
        }
    }
}
