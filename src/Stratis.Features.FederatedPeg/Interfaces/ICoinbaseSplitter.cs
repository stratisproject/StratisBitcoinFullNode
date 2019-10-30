using NBitcoin;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    public interface ICoinbaseSplitter
    {
        /// <summary>
        /// Splits a coinbase's reward into multiple outputs.
        /// </summary>
        void SplitReward(Transaction coinbase);
    }
}
