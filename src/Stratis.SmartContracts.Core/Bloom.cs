using System;
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Type representation of data used in a bloom filter.
    /// </summary>
    public class Bloom : IBitcoinSerializable
    {
        /// <summary>
        /// Length of the bloom data in bytes. 2048 bits.
        /// </summary>
        private const int BloomLength = 256; 

        /// <summary>
        /// The actual bloom value represented as a byte array.
        /// </summary>
        private byte[] data;

        public Bloom()
        {
            this.data = new byte[BloomLength];
        }

        public Bloom(byte[] data)
        {
            if (data?.Length != BloomLength)
                throw new ArgumentException($"Bloom byte array must be {BloomLength} bytes long.", nameof(data));

            this.data = data;
        }

        /// <summary>
        /// Given this and another bloom, bitwise-OR all the data to get a bloom filter representing a range of data.
        /// </summary>
        public void Or(Bloom bloom)
        {
            for (int i = 0; i < this.data.Length; ++i)
            {
                this.data[i] |= bloom.data[i];
            }
        }

        /// <summary>
        /// Add some input to the bloom filter.
        /// </summary>
        /// <remarks>
        ///  From the Ethereum yellow paper (yellowpaper.io):
        ///  M3:2048 is a specialised Bloom filter that sets three bits
        ///  out of 2048, given an arbitrary byte series. It does this through
        ///  taking the low-order 11 bits of each of the first three pairs of
        ///  bytes in a Keccak-256 hash of the byte series.
        /// </remarks>
        public void Add(byte[] input)
        {
            byte[] hashBytes = HashHelper.Keccak256(input);
            // for first 3 pairs, calculate value of first 11 bits
            for (int i = 0; i < 6; i += 2)
            {
                uint low8Bits = (uint)hashBytes[i + 1];
                uint high3Bits = ((uint)hashBytes[i] << 8) & 2047; // AND with 2047 wipes any bits higher than our desired 11.
                uint index = low8Bits + high3Bits;
                this.SetBit((int)index);
            }
        }

        /// <summary>
        /// Determine whether some input is possibly contained within the filter.
        /// </summary>
        /// <param name="test">The byte array to test.</param>
        /// <returns>Whether this data could be contained within the filter.</returns>
        public bool Test(byte[] test)
        {
            var compare = new Bloom();
            compare.Add(test);
            compare.Or(this);
            return this.Equals(compare);
        }

        /// <summary>
        /// Sets the specific bit to 1 within our 256-byte array.
        /// </summary>
        /// <param name="index">Index (0-2047) of the bit to assign to 1.</param>
        private void SetBit(int index)
        {
            int byteIndex = index / 8;
            int bitInByteIndex = index % 8;
            byte mask = (byte)(1 << bitInByteIndex);
            this.data[byteIndex] |= mask;
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

        public override string ToString()
        {
            return this.data.ToHexString();
        }

        public static bool operator ==(Bloom obj1, Bloom obj2)
        {
            if (object.ReferenceEquals(obj1, null))
                return object.ReferenceEquals(obj2, null);

            return Enumerable.SequenceEqual(obj1.data, obj2.data);
        }

        public static bool operator !=(Bloom obj1, Bloom obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as Bloom);
        }

        public bool Equals(Bloom obj)
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
