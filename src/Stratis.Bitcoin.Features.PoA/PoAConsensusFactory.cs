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

        // TODO later implement IBitcoinSerializable for IFederationMember and replace creation from bytes with creation of an empty federation member
        public virtual IFederationMember CreateFederationMemberFromBytes(byte[] serializedBytes)
        {
            var key = new PubKey(serializedBytes);

            IFederationMember federationMember = new FederationMember(key);

            return federationMember;
        }
    }
}
