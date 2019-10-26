namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Interface of coin views that are internally based on other, lower level, implementations of coin view.
    /// For example, we have a cached coin view that can internally use either in-memory coin view or a coin view based on the database.
    /// </summary>
    /// <seealso cref="CoinViewStack"/>
    public interface IBackedCoinView
    {
        /// <summary>Coin view at one layer below this implementaiton.</summary>
        ICoinView Inner { get; }
    }
}
