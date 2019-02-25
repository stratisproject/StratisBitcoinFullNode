using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    /// <summary>
    /// Marks a smart contract network that has the requirement that contracts must be signed.
    /// </summary>
    public interface ISignedCodePubKeyHolder
    {
        PubKey SigningContractPubKey { get; }
    }
}
