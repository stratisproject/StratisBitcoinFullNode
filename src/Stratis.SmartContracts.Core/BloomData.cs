using System;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Type representation of data used in a bloom filter.
    /// </summary>
    public class BloomData : IBitcoinSerializable
    {
        /// <summary>
        /// Length of the bloom data in bytes. 2048 bits.
        /// </summary>
        private const int BloomLength = 256; 

        /// <summary>
        /// The actual bloom value represented as a byte array.
        /// </summary>
        private byte[] data;

        public BloomData()
        {
            this.data = new byte[BloomLength];
        }

        public BloomData(byte[] data)
        {
            if (data?.Length != BloomLength)
                throw new ArgumentException($"Bloom byte array must be {BloomLength} bytes long.", nameof(data));

            this.data = data;
        }

        /// <summary>
        /// Given this and another bloom, bitwise-OR all the data to get a bloom filter representing a range of data.
        /// </summary>
        public void Or(BloomData bloom)
        {
            for (int i = 0; i < this.data.Length; ++i)
            {
                this.data[i] |= bloom.data[i];
            }
        }

        public void ReadWrite(BitcoinStream stream)
        {
            if (stream.Serializing)
            {
                byte[] b = this.data.ToArray(); // doing this cos I'm scared af of that ref...
                stream.ReadWrite(ref b);
            }
            else
            {
                var b = new byte[BloomLength];
                stream.ReadWrite(ref b);
                this.data = b;
            }
        }

        public byte[] ToBytes()
        {
            return this.data;
        }

        public static bool operator ==(BloomData obj1, BloomData obj2)
        {
            if (object.ReferenceEquals(obj1, null))
                return object.ReferenceEquals(obj2, null);

            return Enumerable.SequenceEqual(obj1.data, obj2.data);
        }

        public static bool operator !=(BloomData obj1, BloomData obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as BloomData);
        }

        public bool Equals(BloomData obj)
        {
            if (object.ReferenceEquals(obj, null))
                return false;

            if (object.ReferenceEquals(this, obj))
                return true;

            return (obj == this);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.data);
        }
    }
}
