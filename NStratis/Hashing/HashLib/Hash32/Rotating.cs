﻿using System;
using System.Diagnostics;

namespace HashLib.Hash32
{
    internal class Rotating : Hash, IHash32, IBlockHash
    {
        private uint m_hash;

        public Rotating()
            : base(4, 1)
        {
        }

        public override void Initialize()
        {
            m_hash = 0;
        }

        public override void TransformBytes(byte[] a_data, int a_index, int a_length)
        {
            Debug.Assert(a_index >= 0);
            Debug.Assert(a_length >= 0);
            Debug.Assert(a_index + a_length <= a_data.Length);

            for (int i = a_index; a_length > 0; i++, a_length--)
                m_hash = (m_hash << 4) ^ (m_hash >> 28) ^ a_data[i];
        }

        public override HashResult TransformFinal()
        {
            return new HashResult(m_hash);
        }
    }
}
