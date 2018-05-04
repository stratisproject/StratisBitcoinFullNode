using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConstructorsForSerializableClassesTest
    {
        [Fact]
        public void AssureParameterlessConstructorsExistenceForSerializableClasses()
        {
            // This list contain types that inherit IBitcoinSerializable but don't have a public
            // parameterless constructor for a good reason so they should not fail this test.
            List<Type> exceptionalTypes = new List<Type>()
            {
                typeof(NBitcoin.ExtKey),
                typeof(NBitcoin.ExtPubKey),
                typeof(NBitcoin.PubKey),
                typeof(NBitcoin.PosBlock),
                typeof(NBitcoin.PosBlockHeader),
                typeof(NBitcoin.PosTransaction),
                typeof(NBitcoin.Protocol.CompactVarInt),
            };

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .Where(x =>x.FullName.Contains("Stratis") || x.FullName.Contains("NBitcoin"))
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(IBitcoinSerializable).IsAssignableFrom(p) && !p.IsInterface && p.IsClass);

            foreach (var type in types)
            {
                if (exceptionalTypes.Contains(type))
                    continue;

                bool parameterlessConstructorExists = type.GetConstructors().Any(x => x.GetParameters().Length == 0);

                Assert.True(parameterlessConstructorExists, $"Class {type.FullName} inherits {typeof(IBitcoinSerializable).Name} " +
                    "but doesn't have a parameterless constructor which is needed for serialization and deserialization process!");
            }
        }

        /// <summary>
        /// Serializes input and returns deserialized object.
        /// </summary>
        /// <remarks>Needed for troubleshooting this test.</remarks>
        private T CloneViaSerializeDeserialize<T>(T input) where T : IBitcoinSerializable
        {
            MemoryStream ms = new MemoryStream();
            BitcoinStream bitcoinStream = new BitcoinStream(ms, true);

            input.ReadWrite(bitcoinStream);
            ms.Position = 0;

            bitcoinStream = new BitcoinStream(bitcoinStream.Inner, false);
            var obj = Activator.CreateInstance<T>();
            obj.ReadWrite(bitcoinStream);
            return obj;
        }
    }
}
