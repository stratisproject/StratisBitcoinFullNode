using System;
using System.Collections.Generic;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Executor.Reflection.Persistence;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class MappingTest
    {
        private readonly Dictionary<string, object> underlyingKvStore;

        public MappingTest()
        {
            this.underlyingKvStore = new Dictionary<string, object>();
        }

        [Fact]
        public void Test_NestedMappings()
        {
            IScMapping<IScMapping<string>> mapping = new ScMapping<IScMapping<string>>(this.underlyingKvStore, "MyMapping");

            // Can retrieve nested mappings and set objects in them
            IScMapping<string> mapping2 = mapping["Key1"];
            mapping2["Key2"] = "Value1";
            Assert.Equal("Value1", this.underlyingKvStore["MyMapping[Key1][Key2]"]);

            mapping["Key1"]["Key3"] = "Value2";
            Assert.Equal("Value2", mapping2["Key3"]);

            // Can't set mapping
            Assert.Throws<NotSupportedException>(() => mapping["test"] = new ScMapping<string>(this.underlyingKvStore, "MyMapping2"));
        }

        [Fact]
        public void Test_List()
        {
            IScList<string> list = new ScList<string>(this.underlyingKvStore, "List");
            Assert.Equal(0, list.Count);

            list.Push("Test1");
            list.Push("Test2");
            Assert.Equal(2, list.Count);
            Assert.Equal("Test1", list[0]);
            Assert.Equal("Test2", list[1]);
        }

        [Fact]
        public void Test_MappingOfLists()
        {
            IScMapping<IScList<string>> mappingOfLists = new ScMapping<IScList<string>>(this.underlyingKvStore, "MyMapping");

            IScList<string> aList = mappingOfLists["SomeValue"];
            Assert.Equal(0, aList.Count);

            aList.Push("Value1");
            Assert.Equal("Value1", mappingOfLists["SomeValue"][0]);
            Assert.Equal("Value1", this.underlyingKvStore["MyMapping[SomeValue][0]"]);

            Assert.Throws<NotSupportedException>(() => mappingOfLists["SomeValue"] = new ScList<string>(this.underlyingKvStore, "newName"));
        }
    }
}
