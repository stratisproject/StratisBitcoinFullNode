using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public class TemplateTransaction : IBitcoinSerializable
    {
        private uint256 hash;

        public uint256 Hash { get { return this.hash; } set { this.hash = value; } }

        public TemplateTransaction(uint256 hash)
        {
            this.Hash = hash;
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.hash);
        }
    }
}
