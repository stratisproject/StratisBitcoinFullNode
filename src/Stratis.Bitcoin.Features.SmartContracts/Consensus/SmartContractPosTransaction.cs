using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>
    /// A smart contract proof of stake transaction.
    /// </summary>
    /// <remarks>
    /// TODO: later we can move the POS timestamp field in this class.
    /// serialization can be refactored to have a common array that will be serialized and each inheritance can add to the array)
    /// </remarks>
    public class SmartContractPosTransaction : Transaction
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