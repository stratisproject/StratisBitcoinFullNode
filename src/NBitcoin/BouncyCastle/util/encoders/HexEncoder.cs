using System.IO;

namespace Stratis.Bitcoin.NBitcoin.BouncyCastle.Utilities.Encoders
{
    internal class HexEncoder
        : IEncoder
    {
        protected readonly byte[] encodingTable =
        {
            (byte)'0', (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'5', (byte)'6', (byte)'7',
            (byte)'8', (byte)'9', (byte)'a', (byte)'b', (byte)'c', (byte)'d', (byte)'e', (byte)'f'
        };

        /*
         * set up the decoding table.
         */
        protected readonly byte[] decodingTable = new byte[128];

        protected void InitialiseDecodingTable()
        {
            Arrays.Fill(this.decodingTable, (byte)0xff);

            for(int i = 0; i < this.encodingTable.Length; i++)
            {
                this.decodingTable[this.encodingTable[i]] = (byte)i;
            }

            this.decodingTable['A'] = this.decodingTable['a'];
            this.decodingTable['B'] = this.decodingTable['b'];
            this.decodingTable['C'] = this.decodingTable['c'];
            this.decodingTable['D'] = this.decodingTable['d'];
            this.decodingTable['E'] = this.decodingTable['e'];
            this.decodingTable['F'] = this.decodingTable['f'];
        }

        public HexEncoder()
        {
            InitialiseDecodingTable();
        }

        /**
        * encode the input data producing a Hex output stream.
        *
        * @return the number of bytes produced.
        */
        public int Encode(
            byte[] data,
            int off,
            int length,
            Stream outStream)
        {
            for(int i = off; i < (off + length); i++)
            {
                int v = data[i];

                outStream.WriteByte(this.encodingTable[v >> 4]);
                outStream.WriteByte(this.encodingTable[v & 0xf]);
            }

            return length * 2;
        }

        private static bool Ignore(char c)
        {
            return c == '\n' || c == '\r' || c == '\t' || c == ' ';
        }

        /**
        * decode the Hex encoded byte data writing it to the given output stream,
        * whitespace characters will be ignored.
        *
        * @return the number of bytes produced.
        */
        public int Decode(
            byte[] data,
            int off,
            int length,
            Stream outStream)
        {
            byte b1, b2;
            int outLen = 0;
            int end = off + length;

            while(end > off)
            {
                if(!Ignore((char)data[end - 1]))
                {
                    break;
                }

                end--;
            }

            int i = off;
            while(i < end)
            {
                while(i < end && Ignore((char)data[i]))
                {
                    i++;
                }

                b1 = this.decodingTable[data[i++]];

                while(i < end && Ignore((char)data[i]))
                {
                    i++;
                }

                b2 = this.decodingTable[data[i++]];

                if((b1 | b2) >= 0x80)
                    throw new IOException("invalid characters encountered in Hex data");

                outStream.WriteByte((byte)((b1 << 4) | b2));

                outLen++;
            }

            return outLen;
        }

        /**
        * decode the Hex encoded string data writing it to the given output stream,
        * whitespace characters will be ignored.
        *
        * @return the number of bytes produced.
        */
        public int DecodeString(
            string data,
            Stream outStream)
        {
            byte b1, b2;
            int length = 0;

            int end = data.Length;

            while(end > 0)
            {
                if(!Ignore(data[end - 1]))
                {
                    break;
                }

                end--;
            }

            int i = 0;
            while(i < end)
            {
                while(i < end && Ignore(data[i]))
                {
                    i++;
                }

                b1 = this.decodingTable[data[i++]];

                while(i < end && Ignore(data[i]))
                {
                    i++;
                }

                b2 = this.decodingTable[data[i++]];

                if((b1 | b2) >= 0x80)
                    throw new IOException("invalid characters encountered in Hex data");

                outStream.WriteByte((byte)((b1 << 4) | b2));

                length++;
            }

            return length;
        }
    }
}
