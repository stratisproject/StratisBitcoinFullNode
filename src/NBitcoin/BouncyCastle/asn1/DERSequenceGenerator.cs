using System.IO;

namespace NBitcoin.BouncyCastle.Asn1
{
    internal class DerSequenceGenerator
        : DerGenerator
    {
        private readonly MemoryStream _bOut = new MemoryStream();

        public DerSequenceGenerator(
            Stream outStream)
            : base(outStream)
        {
        }

        public DerSequenceGenerator(
            Stream outStream,
            int tagNo,
            bool isExplicit)
            : base(outStream, tagNo, isExplicit)
        {
        }

        public override void AddObject(
            Asn1Encodable obj)
        {
            new DerOutputStream(this._bOut).WriteObject(obj);
        }

        public override Stream GetRawOutputStream()
        {
            return this._bOut;
        }

        public override void Close()
        {
            WriteDerEncoded(Asn1Tags.Constructed | Asn1Tags.Sequence, this._bOut.ToArray());
        }
    }
}
