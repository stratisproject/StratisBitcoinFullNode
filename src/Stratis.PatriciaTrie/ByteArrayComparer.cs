using System;
using System.Collections.Generic;

namespace Stratis.PatriciaTrie
{
    /// <summary>
    /// Used to compare byte arrays in dictionaries. They are otherwise compared via reference, which isn't very useful for us.
    /// </summary>
    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }
            if (left.Length != right.Length)
            {
                return false;
            }
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }
            return true;
        }
        public int GetHashCode(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            int sum = 0;
            foreach (byte cur in key)
            {
                sum += cur;
            }
            return sum;
        }
    }
}
