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
        public uint160 From { get; set; }
        public uint160 To { get; set; }
        public ulong Value { get; set; }
    }
}
