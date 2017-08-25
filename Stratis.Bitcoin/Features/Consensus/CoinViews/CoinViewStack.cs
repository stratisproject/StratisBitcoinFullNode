using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
    /// <summary>
    /// Stack of coinview layers. All classes in the stack have to be based on <see cref="CoinView"/> class
    /// and all classes except for the stack bottom class have to implement <see cref="IBackedCoinView"/>
    /// interface.
    /// </summary>
    public class CoinViewStack
    {
        /// <summary>Coinview class at the top of the stack.</summary>
        public CoinView Top { get; private set; }

        /// <summary>Coinview class at the bottom of the stack.</summary>
        public CoinView Bottom { get; private set; }

        /// <summary>
        /// Initializes an instance of the stack using existing coinview.
        /// </summary>
        /// <param name="top">Coinview at the top of the stack.</param>
        public CoinViewStack(CoinView top)
        {
            this.Top = top;
            CoinView current = top;
            while (current is IBackedCoinView)
            {
                current = ((IBackedCoinView)current).Inner;
            }
            this.Bottom = current;
        }

        /// <summary>
        /// Enumerates coinviews in the stack ordered from the top to the bottom.
        /// </summary>
        /// <returns>Enumeration of coin views in the stack ordered from the top to the bottom.</returns>
        public IEnumerable<CoinView> GetElements()
        {
            CoinView current = this.Top;
            while (current is IBackedCoinView)
            {
                yield return current;
                current = ((IBackedCoinView)current).Inner;
            }
            yield return current;
        }

        /// <summary>
        /// Finds a coinview of specific type in the stack.
        /// </summary>
        /// <typeparam name="T">Type of the coinview to search for.</typeparam>
        /// <returns>Coinview of the specific type from the stack or <c>null</c> if such a coinview is not in the stack.</returns>
        public T Find<T>()
        {
            CoinView current = this.Top;
            if (current is T)
                return (T)(object)current;

            while (current is IBackedCoinView)
            {
                current = ((IBackedCoinView)current).Inner;
                if (current is T)
                    return (T)(object)current;
            }

            return default(T);
        }
    }
}
