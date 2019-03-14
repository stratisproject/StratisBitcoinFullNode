using System.IO;

namespace Stratis.Bitcoin.NBitcoin.BouncyCastle.Asn1
{
    internal interface Asn1OctetStringParser
        : IAsn1Convertible
    {
        Stream GetOctetStream();
    }
}
