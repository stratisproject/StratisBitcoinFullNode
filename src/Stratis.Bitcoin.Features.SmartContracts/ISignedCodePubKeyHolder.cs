using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISignedCodePubKeyHolder
    {
        PubKey SigningContractPubKey { get; }
    }
}
