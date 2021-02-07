using NBitcoin;
using System.IO;
using System.Text;

namespace LedgerWallet
{
    public class GetWalletPubKeyResponse
    {
        public GetWalletPubKeyResponse(byte[] bytes)
        {
            var ms = new MemoryStream(bytes);
            var len = ms.ReadByte();
            UncompressedPublicKey = new PubKey(ms.ReadBytes(len));
            len = ms.ReadByte();
            var addr = Encoding.ASCII.GetString(ms.ReadBytes(len));
            Address = addr;
            ChainCode = ms.ReadBytes(32);
            ExtendedPublicKey = new ExtPubKey(UncompressedPublicKey.Compress(), ChainCode);
        }

        public ExtPubKey ExtendedPublicKey
        {
            get;
            set;
        }

        public PubKey UncompressedPublicKey
        {
            get;
            set;
        }

        public BitcoinAddress GetAddress(Network network)
        {
            return BitcoinAddress.Create(Address, network);
        }

        public string Address
        {
            get;
            set;
        }

        public byte[] ChainCode
        {
            get;
            set;
        }
    }
}
