namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    public interface IBackedCoinView
    {
        CoinView Inner
        {
            get;
        }
    }
}
