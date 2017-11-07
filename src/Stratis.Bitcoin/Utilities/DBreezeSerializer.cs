using System;
using System.Linq;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Implementation of serialization and deserialization of objects that go into the DBreeze database.
    /// </summary>
    public class DBreezeSerializer
    {
        /// <summary>
        /// Initializes custom serializers for DBreeze engine.
        /// </summary>
        public void Initialize()
        {
            CustomSerializator.ByteArraySerializator = this.Serializer;
            CustomSerializator.ByteArrayDeSerializator = this.Deserializer;
        }

        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
        internal byte[] Serializer(object obj)
        {
            IBitcoinSerializable serializable = obj as IBitcoinSerializable;
            if (serializable != null)
                return serializable.ToBytes();

            uint256 u256 = obj as uint256;
            if (u256 != null)
                return u256.ToBytes();

            uint160 u160 = obj as uint160;
            if (u160 != null)
                return u160.ToBytes();

            uint? u32 = obj as uint?;
            if (u32 != null)
                return u32.ToBytes();

            object[] arr = obj as object[];
            if (arr != null)
            {
                byte[][] serializedItems = new byte[arr.Length][];
                int itemIndex = 0;
                foreach (object arrayObject in arr)
                {
                    byte[] serializedObject = Serializer(arrayObject);
                    serializedItems[itemIndex] = serializedObject;
                    itemIndex++;
                }
                return ConcatArrays(serializedItems);
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Concatenates multiple byte arrays into a single byte array.
        /// </summary>
        /// <param name="arrays">Arrays to concatenate.</param>
        /// <returns>Concatenation of input arrays.</returns>
        /// <remarks>Based on https://stackoverflow.com/a/415396/3835864 .</remarks>
        private static byte[] ConcatArrays(byte[][] arrays)
        {
            byte[] res = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, res, offset, array.Length);
                offset += array.Length;
            }
            return res;
        }

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
        internal object Deserializer(byte[] bytes, Type type)
        {
            if (type == typeof(Coins))
            {
                Coins coin = new Coins();
                coin.ReadWrite(bytes);
                return coin;
            }

            if (type == typeof(BlockHeader))
            {
                BlockHeader header = new BlockHeader();
                header.ReadWrite(bytes);
                return header;
            }

            if (type == typeof(RewindData))
            {
                RewindData rewind = new RewindData();
                rewind.ReadWrite(bytes);
                return rewind;
            }

            if (type == typeof(uint256))
                return new uint256(bytes);

            if (type == typeof(Block))
                return new Block(bytes);

            if (type == typeof(BlockStake))
                return new BlockStake(bytes);

            throw new NotSupportedException();
        }
    }
}