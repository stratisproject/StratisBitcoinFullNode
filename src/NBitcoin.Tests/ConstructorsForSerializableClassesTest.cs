using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NBitcoin.Tests
{
    public class ConstructorsForSerializableClassesTest
    {
        [Fact]
        public void AssureParameterlessConstructorsExistenceForSerializableClasses()
        {
            // This list contain types that inherit IBitcoinSerializable but don't have a public
            // parameterless constructor for a good reason so they should not fail this test.
            var exceptionalTypes = new List<Type>()
            {
                typeof(BlockHeader),
                typeof(ProvenBlockHeader),
                typeof(ExtKey),
                typeof(ExtPubKey),
                typeof(PubKey),
                typeof(PosBlock),
                typeof(PosBlockHeader),
                typeof(PosTransaction),
                typeof(Protocol.CompactVarInt),
            };

            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x => x.FullName.Contains("Stratis") || x.FullName.Contains("NBitcoin"))
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IBitcoinSerializable).IsAssignableFrom(p) && !p.IsInterface && p.IsClass);

            foreach (Type type in types)
            {
                if (exceptionalTypes.Contains(type))
                    continue;

                bool parameterlessConstructorExists = type.GetConstructors().Any(x => x.GetParameters().Length == 0);

                Assert.True(parameterlessConstructorExists, $"Class {type.FullName} inherits {typeof(IBitcoinSerializable).Name} " +
                    "but doesn't have a parameterless constructor which is needed for serialization and deserialization process!");
            }
        }
    }
}
