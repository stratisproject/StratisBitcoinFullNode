using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin.Indexer
{
    public class FastEncoder
    {
        private static readonly FastEncoder _Instance = new FastEncoder();
        public static FastEncoder Instance
        {
            get
            {
                return _Instance;
            }
        }
        public FastEncoder()
        {
            var ranges = new int[][]{
                new int[]{0x20,0x22},
                new int[]{0x26,0x2C},
                new int[]{0x30,0x3E},
                new int[]{0x40,0x5B},
                new int[]{0x5D,0x7E},
                new int[]{0xA0,0x148}
             };
            var unicodes = Enumerate(ranges);
            StringBuilder builder = new StringBuilder(260);
            foreach (var i in unicodes)
            {
                builder.Append((char)i);
            }

            _BytesToChar = builder.ToString().ToCharArray();
            _CharToBytes = new byte[ranges[ranges.Length - 1][1] + 1];

            var enumerator = unicodes.GetEnumerator();
            for (int i = 0 ; i < 256 ; i++)
            {
                enumerator.MoveNext();
                _CharToBytes[enumerator.Current] = (byte)i;
            }
        }

        private IEnumerable<int> Enumerate(int[][] ranges)
        {
            foreach (var range in ranges)
            {
                for (int i = range[0] ; i <= range[1] ; i++)
                    yield return i;
            }
        }

        readonly char[] _BytesToChar;
        readonly byte[] _CharToBytes;
        public byte[] DecodeData(string encoded)
        {
            var result = new byte[encoded.Length];
            int i = 0;
            foreach (var c in encoded.ToCharArray())
            {
                result[i] = _CharToBytes[c];
                i++;
            }
            return result;
        }

        public string EncodeData(byte[] data, int offset, int length)
        {
            char[] result = new char[length];
            for (int i = 0 ; i < length ; i++)
            {
                result[i] = _BytesToChar[data[offset + i]];
            }
            return new String(result);
        }

        public string EncodeData(byte[] data)
        {
            return EncodeData(data, 0, data.Length);
        }
    }
}
