using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override Block CreateBlock()
        {
            return new Block(this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new PoABlockHeader();
        }

        public virtual IFederationMember DeserializeFederationMember(byte[] serializedBytes)
        {
            var key = new PubKey(serializedBytes);

            IFederationMember federationMember = new FederationMember(key);

            return federationMember;
        }

        public virtual byte[] SerializeFederationMember(IFederationMember federationMember)
        {
            return federationMember.PubKey.ToBytes();
        }
    }
}
