namespace Stratis.Bitcoin.Features.Wallet.Broadcasting
{
    public enum TransactionBroadcastState
    {
        CantBroadcast,
        ToBroadcast,
        Broadcasted,
        Propagated
    }
}
