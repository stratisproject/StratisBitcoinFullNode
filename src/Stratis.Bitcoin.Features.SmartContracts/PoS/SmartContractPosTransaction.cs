using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    /// <summary>A smart contract proof of stake transaction.</summary>
    public class SmartContractPosTransaction : PosTransaction
    {
        public SmartContractPosTransaction() : base()
        {
        }

        public SmartContractPosTransaction(string hex, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION) : this()
        {
            this.FromBytes(Encoders.Hex.DecodeData(hex), version);
        }

        public SmartContractPosTransaction(byte[] bytes) : this()
        {
            this.FromBytes(bytes);
        }
    }
}