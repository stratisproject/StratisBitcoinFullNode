using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze.Utils;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Implementation of serialization and deserialization of objects that go into the DBreeze database.
    /// </summary>
    public class DBreezeSerializer
    {
        public DBreezeSerializer(Network network)
        {
            this.Network = network;
        }

        public Network Network { get; }

        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
        public byte[] Serialize(object obj)
        {
            if (obj is IBitcoinSerializable serializable)
                return serializable.ToBytes(this.Network.Consensus.ConsensusFactory);

            if (obj is uint256 u256)
                return u256.ToBytes();

            if (obj is uint160 u160)
                return u160.ToBytes();

            if (obj is uint u32)
                return u32.ToBytes();

            if (obj is IEnumerable<object> collection)
            {
                object[] array = obj as object[] ?? collection.ToArray();

                var serializedItems = new byte[array.Length][];
                int itemIndex = 0;
                foreach (object arrayObject in array)
                {
                    byte[] serializedObject = this.Serialize(arrayObject);
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
            var res = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, res, offset, array.Length);
                offset += array.Length;
            }

            return res;
        }

        public T Deserialize<T>(byte[] bytes)
        {
            return (T) this.Deserialize(bytes, typeof(T));
        }

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
        public object Deserialize(byte[] bytes, Type type)
        {
            if (type == typeof(BlockHeader))
            {
                BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.ReadWrite(bytes, this.Network.Consensus.ConsensusFactory);
                return header;
            }

            if (type == typeof(Transaction))
            {
                Transaction transaction = this.Network.Consensus.ConsensusFactory.CreateTransaction();
                transaction.ReadWrite(bytes, this.Network.Consensus.ConsensusFactory);
                return transaction;
            }

            if (type == typeof(uint256))
                return new uint256(bytes);

            if (type == typeof(Block))
                return Block.Load(bytes, this.Network);

            if (type == typeof(BlockStake))
                return BlockStake.Load(bytes, this.Network);

            if (type == typeof(ProvenBlockHeader))
            {
                ProvenBlockHeader provenBlockHeader =
                    ((PosConsensusFactory)this.Network.Consensus.ConsensusFactory).CreateProvenBlockHeader();

                provenBlockHeader.ReadWrite(bytes, this.Network.Consensus.ConsensusFactory);
                return provenBlockHeader;
            }

            if (type == typeof(HashHeightPair))
                return HashHeightPair.Load(bytes, this.Network);

            if (typeof(IBitcoinSerializable).IsAssignableFrom(type))
            {
                var result = (IBitcoinSerializable)Activator.CreateInstance(type);
                result.ReadWrite(bytes, this.Network.Consensus.ConsensusFactory);
                return result;
            }

            throw new NotSupportedException();
        }
    }
}