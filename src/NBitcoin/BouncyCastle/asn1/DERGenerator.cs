using System.IO;
using NBitcoin.BouncyCastle.Utilities.IO;

namespace NBitcoin.BouncyCastle.Asn1
{
    internal abstract class DerGenerator
        : Asn1Generator
    {
        private bool _tagged = false;
        private bool _isExplicit;
        private int _tagNo;

        protected DerGenerator(
            Stream outStream)
            : base(outStream)
        {
        }

        protected DerGenerator(
            Stream outStream,
            int tagNo,
            bool isExplicit)
            : base(outStream)
        {
            this._tagged = true;
            this._isExplicit = isExplicit;
            this._tagNo = tagNo;
        }

        private static void WriteLength(
            Stream outStr,
            int length)
        {
            if(length > 127)
            {
                int size = 1;
                int val = length;

                while((val >>= 8) != 0)
                {
                    size++;
                }

                outStr.WriteByte((byte)(size | 0x80));

                for(int i = (size - 1) * 8; i >= 0; i -= 8)
                {
                    outStr.WriteByte((byte)(length >> i));
                }
            }
            else
            {
                outStr.WriteByte((byte)length);
            }
        }

        internal static void WriteDerEncoded(
            Stream outStream,
            int tag,
            byte[] bytes)
        {
            outStream.WriteByte((byte)tag);
            WriteLength(outStream, bytes.Length);
            outStream.Write(bytes, 0, bytes.Length);
        }

        internal void WriteDerEncoded(
            int tag,
            byte[] bytes)
        {
            if(this._tagged)
            {
                int tagNum = this._tagNo | Asn1Tags.Tagged;

                if(this._isExplicit)
                {
                    int newTag = this._tagNo | Asn1Tags.Constructed | Asn1Tags.Tagged;
                    var bOut = new MemoryStream();
                    WriteDerEncoded(bOut, tag, bytes);
                    WriteDerEncoded(this.Out, newTag, bOut.ToArray());
                }
                else
                {
                    if((tag & Asn1Tags.Constructed) != 0)
                    {
                        tagNum |= Asn1Tags.Constructed;
                    }

                    WriteDerEncoded(this.Out, tagNum, bytes);
                }
            }
            else
            {
                WriteDerEncoded(this.Out, tag, bytes);
            }
        }

        internal static void WriteDerEncoded(
            Stream outStr,
            int tag,
            Stream inStr)
        {
            WriteDerEncoded(outStr, tag, Streams.ReadAll(inStr));
        }
    }
}
