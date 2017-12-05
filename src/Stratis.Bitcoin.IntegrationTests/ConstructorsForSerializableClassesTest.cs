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
                typeof(NBitcoin.Protocol.CompactVarInt),
                typeof(NBitcoin.BitcoinCore.StoredBlock),
                typeof(NBitcoin.BitcoinCore.StoredItem<>)
            };

            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x =>
                x.FullName.Contains("Stratis") || x.FullName.Contains("NBitcoin")).ToList();

            Assert.True(assemblies.Count != 0);

            List<Type> serializableTypes = new List<Type>();

            //collect all types that inherit IBitcoinSerializable
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var interfaces = type.GetInterfaces();
                    if (interfaces.Contains(typeof(IBitcoinSerializable)))
                        serializableTypes.Add(type);
                }
            }

            //ensure each type implements a parameterless constructor.
            foreach (var type in serializableTypes)
            {
                if (!type.IsClass)
                    continue;

                bool parameterlessConstructorExists = type.GetConstructors().Any(x => x.GetParameters().Length == 0);

                if (!exceptionalTypes.Contains(type))
                {
                    Assert.True(parameterlessConstructorExists, $"Class {type.FullName} inherits {typeof(IBitcoinSerializable).Name} " +
                        "but doesn't have a parameterless constructor which is needed for serialization and deserialization process!");
                }
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
