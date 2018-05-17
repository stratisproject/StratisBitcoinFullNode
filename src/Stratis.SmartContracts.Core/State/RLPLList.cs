using System;
using System.Linq;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Core.State
{
    /// <summary>
    /// Adapted from EthereumJ. Used to store data inside of the PatriciaTrie.
    /// </summary>
    public class RLPLList
    {
        /**
     * [0x80]
     * If a string is 0-55 bytes long, the RLP encoding consists of a single
     * byte with value 0x80 plus the length of the string followed by the
     * string. The range of the first byte is thus [0x80, 0xb7].
     */
        private static int OFFSET_SHORT_ITEM = 0x80;

        /**
         * [0xb7]
         * If a string is more than 55 bytes long, the RLP encoding consists of a
         * single byte with value 0xb7 plus the length of the length of the string
         * in binary form, followed by the length of the string, followed by the
         * string. For example, a length-1024 string would be encoded as
         * \xb9\x04\x00 followed by the string. The range of the first byte is thus
         * [0xb8, 0xbf].
         */
        private static int OFFSET_LONG_ITEM = 0xb7;

        /**
         * [0xc0]
         * If the total payload of a list (i.e. the combined length of all its
         * items) is 0-55 bytes long, the RLP encoding consists of a single byte
         * with value 0xc0 plus the length of the list followed by the concatenation
         * of the RLP encodings of the items. The range of the first byte is thus
         * [0xc0, 0xf7].
         */
        private static int OFFSET_SHORT_LIST = 0xc0;

        /**
         * [0xf7]
         * If the total payload of a list is more than 55 bytes long, the RLP
         * encoding consists of a single byte with value 0xf7 plus the length of the
         * length of the list in binary form, followed by the length of the list,
         * followed by the concatenation of the RLP encodings of the items. The
         * range of the first byte is thus [0xf8, 0xff].
         */
        private static int OFFSET_LONG_LIST = 0xf7;
        private byte[] rlp;
        private int[] offsets = new int[32];
        private int[] lens = new int[32];
        private int cnt;

        public RLPLList(byte[] rlp)
        {
            this.rlp = rlp;
        }

        public byte[] GetEncoded()
        {
            byte[][] encoded = new byte[this.cnt][];
            for (int i = 0; i< this.cnt; i++) {
                encoded[i] = RLP.EncodeElement(this.GetBytes(i));
            }
            return RLP.EncodeList(encoded);
        }


        public void Add(int off, int len, bool isList)
        {
            this.offsets[this.cnt] = off;
            this.lens[this.cnt] = isList ? (-1 - len) : len;
            this.cnt++;
        }

        public byte[] GetBytes(int idx)
        {
            int len = this.lens[idx];
            len = len < 0 ? (-len - 1) : len;
            byte[] ret = new byte[len];
            Array.Copy(this.rlp, this.offsets[idx], ret, 0, len);
            return ret;
        }

        public RLPLList GetList(int idx)
        {
            return DecodeLazyList(this.rlp, this.offsets[idx], -this.lens[idx] - 1);
        }

        public bool IsList(int idx)
        {
            return this.lens[idx] < 0;
        }

        public int Size()
        {
            return this.cnt;
        }

        public static RLPLList DecodeLazyList(byte[] data)
        {
            return DecodeLazyList(data, 0, data.Length).GetList(0);
        }

        public static RLPLList DecodeLazyList(byte[] data, int pos, int length)
        {
            if (data == null || data.Length < 1)
            {
                return null;
            }
            RLPLList ret = new RLPLList(data);
            int end = pos + length;

            while (pos < end)
            {
                int prefix = data[pos] & 0xFF;
                if (prefix == OFFSET_SHORT_ITEM)
                {  // 0x80
                    ret.Add(pos, 0, false); // means no length or 0
                    pos++;
                }
                else if (prefix < OFFSET_SHORT_ITEM)
                {  // [0x00, 0x7f]
                    ret.Add(pos, 1, false); // means no length or 0
                    pos++;
                }
                else if (prefix <= OFFSET_LONG_ITEM)
                {  // [0x81, 0xb7]
                    int len = prefix - OFFSET_SHORT_ITEM; // length of the encoded bytes
                    ret.Add(pos + 1, len, false);
                    pos += len + 1;
                }
                else if (prefix < OFFSET_SHORT_LIST)
                {  // [0xb8, 0xbf]
                    int lenlen = prefix - OFFSET_LONG_ITEM; // length of length the encoded bytes
                    byte[] copy = new byte[lenlen];
                    Array.Copy(data, pos + 1, copy,0, lenlen);
                    copy = copy.Reverse().ToArray();
                    copy = PadBytesForInt(copy);
                    int lenbytes =  BitConverter.ToInt32(copy, 0); // length of encoded bytes
                    ret.Add(pos + 1 + lenlen, lenbytes, false);
                    pos += 1 + lenlen + lenbytes;
                }
                else if (prefix <= OFFSET_LONG_LIST)
                {  // [0xc0, 0xf7]
                    int len = prefix - OFFSET_SHORT_LIST; // length of the encoded list
                    ret.Add(pos + 1, len, true);
                    pos += 1 + len;
                }
                else if (prefix <= 0xFF)
                {  // [0xf8, 0xff]
                    int lenlen = prefix - OFFSET_LONG_LIST; // length of length the encoded list
                    byte[] copy = new byte[lenlen];
                    Array.Copy(data, pos + 1, copy, 0, lenlen);
                    copy = copy.Reverse().ToArray();
                    copy = PadBytesForInt(copy);
                    int lenlist = BitConverter.ToInt32(copy, 0); // length of encoded bytes
                    ret.Add(pos + 1 + lenlen, lenlist, true);
                    pos += 1 + lenlen + lenlist; // start at position of first element in list
                }
                else
                {
                    throw new Exception("Only byte values between 0x00 and 0xFF are supported, but got: " + prefix);
                }
            }
            return ret;
        }

        private static byte[] PadBytesForInt(byte[] bytes)
        {
            if (bytes.Length < 4)
            {
                byte[] temp = new byte[4];
                bytes.CopyTo(temp, 0);
                bytes = temp;
            }
            return bytes;
        }
    }
}
