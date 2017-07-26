﻿using System;
using System.Diagnostics;

namespace HashLib.Hash32
{
    internal class ELF : Hash, IHash32, IBlockHash
    {
        private uint m_hash;

        public ELF()
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
            {
                m_hash = (m_hash << 4) + a_data[i];
                uint g = m_hash & 0xf0000000;

                if (g != 0)
                    m_hash ^= g >> 24;

                m_hash &= ~g;
            }
        }

        public override HashResult TransformFinal()
        {
            return new HashResult(m_hash);
        }
    }
}
