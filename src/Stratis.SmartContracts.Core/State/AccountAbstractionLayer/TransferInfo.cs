using NBitcoin;

namespace Stratis.SmartContracts.Core.State.AccountAbstractionLayer
{
    /// <summary>
    /// Every time we send a transfer from one address to another inside of a contract,
    /// we need to store the transfer information so that we can build a condensing transaction
    /// to reconcile all the UTXOs afterwards.
    /// </summary>
    public class TransferInfo
    {
        public TransferInfo(uint160 from, uint160 to, ulong value)
        {
            this.From = from;
            this.To = to;
            this.Value = value;
        }

        public uint160 From { get; }
        public uint160 To { get; }
        public ulong Value { get; }
    }
}
