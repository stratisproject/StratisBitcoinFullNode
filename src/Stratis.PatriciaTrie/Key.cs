using System;

namespace Stratis.Patricia
{
    internal sealed class Key
    {
        public const int ODD_OFFSET_FLAG = 0x1;
        public const int TERMINATOR_FLAG = 0x2;
        private readonly byte[] key;
        private readonly int off;

        public bool IsTerminal { get; set; }

        public int Length
        {
            get
            {
                return (this.key.Length << 1) - this.off;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return this.Length == 0;
            }
        }

        public static Key FromNormal(byte[] key)
        {
            return new Key(key);
        }

        public static Key FromPacked(byte[] key)
        {
            return new Key(key, ((key[0] >> 4) & ODD_OFFSET_FLAG) != 0 ? 1 : 2, ((key[0] >> 4) & TERMINATOR_FLAG) != 0);
        }

        public static Key Empty(bool terminal)
        {
            return new Key(new byte[0], 0, terminal);
        }

        public static Key SingleHex(int hex)
        {
            Key ret = new Key(new byte[1], 1, false);
            ret.SetHex(0, hex);
            return ret;
        }

        public Key(byte[] key, int off = 0, bool terminal = true)
        {
            this.IsTerminal = terminal;
            this.off = off;
            this.key = key;
        }

        public byte[] ToPacked()
        {
            int flags = ((this.off & 1) != 0 ? ODD_OFFSET_FLAG : 0) | (this.IsTerminal ? TERMINATOR_FLAG : 0);
            byte[] ret = new byte[this.Length / 2 + 1];
            int toCopy = (flags & ODD_OFFSET_FLAG) != 0 ? ret.Length : ret.Length - 1;
            Array.Copy(this.key, this.key.Length - toCopy, ret, ret.Length - toCopy, toCopy); // absolutely no idea if this is right
            ret[0] &= 0x0F;
            ret[0] |= (byte) (flags << 4);
            return ret;
        }

        public int GetHex(int idx)
        {
            int adjustedIndex = (this.off + idx) >> 1;
            byte b = this.key[(this.off + idx) >> 1];
            return (((this.off + idx) & 1) == 0 ? (b >> 4) : b) & 0xF;
        }

        public Key Shift(int hexCnt)
        {
            return new Key(this.key, this.off + hexCnt, this.IsTerminal);
        }

        private void SetHex(int idx, int hex)
        {
            int byteIdx = (this.off + idx) >> 1;
            if (((this.off + idx) & 1) == 0)
            {
                this.key[byteIdx] &= 0x0F;
                this.key[byteIdx] |= (byte) (hex << 4);
            }
            else
            {
                this.key[byteIdx] &= 0xF0;
                this.key[byteIdx] |= (byte) hex;
            }
        }

        public Key MatchAndShift(Key k)
        {
            int len = this.Length;
            int kLen = k.Length;
            if (len < kLen) return null;

            if ((this.off & 1) == (k.off & 1))
            {
                // optimization to compare whole keys bytes
                if ((this.off & 1) == 1)
                {
                    if (this.GetHex(0) != k.GetHex(0)) return null;
                }
                int idx1 = (this.off + 1) >> 1;
                int idx2 = (k.off + 1) >> 1;
                int l = kLen >> 1;
                for (int i = 0; i < l; i++, idx1++, idx2++)
                {
                    if (this.key[idx1] != k.key[idx2]) return null;
                }
            }
            else
            {
                for (int i = 0; i < kLen; i++)
                {
                    if (this.GetHex(i) != k.GetHex(i)) return null;
                }
            }
            return this.Shift(kLen);
        }

        public Key Concat(Key k)
        {
            if (this.IsTerminal) throw new PatriciaTreeResolutionException("Can't append to terminal key: " + this + " + " + k);
            int len = this.Length;
            int kLen = k.Length;
            int newLen = len + kLen;
            byte[] newKeyBytes = new byte[(newLen + 1) >> 1];
            Key ret = new Key(newKeyBytes, newLen & 1, k.IsTerminal);
            for (int i = 0; i < len; i++)
            {
                ret.SetHex(i, this.GetHex(i));
            }
            for (int i = 0; i < kLen; i++)
            {
                ret.SetHex(len + i, k.GetHex(i));
            }
            return ret;
        }

        public Key GetCommonPrefix(Key k)
        {
            // TODO can be optimized
            int prefixLen = 0;
            int thisLenght = this.Length;
            int kLength = k.Length;
            while (prefixLen < thisLenght && prefixLen < kLength && this.GetHex(prefixLen) == k.GetHex(prefixLen))
                prefixLen++;
            byte[] prefixKey = new byte[(prefixLen + 1) >> 1];
            Key ret = new Key(prefixKey, (prefixLen & 1) == 0 ? 0 : 1,
                    prefixLen == this.Length && prefixLen == k.Length && this.IsTerminal && k.IsTerminal);
            for (int i = 0; i < prefixLen; i++)
            {
                ret.SetHex(i, k.GetHex(i));
            }
            return ret;
        }

        public override bool Equals(object obj)
        {
            Key k = (Key)obj;
            int len = this.Length;

            if (len != k.Length) return false;
            // TODO can be optimized
            for (int i = 0; i < len; i++)
            {
                if (this.GetHex(i) != k.GetHex(i)) return false;
            }
            return this.IsTerminal == k.IsTerminal;
        }

    }
}
