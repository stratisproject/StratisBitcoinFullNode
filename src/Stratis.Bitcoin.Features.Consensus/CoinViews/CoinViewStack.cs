using System.Collections.Generic;
using Stratis.Bitcoin.Utilities;

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
        public ICoinView Top { get; private set; }

        /// <summary>Coinview class at the bottom of the stack.</summary>
        public ICoinView Bottom { get; private set; }

        /// <summary>
        /// Initializes an instance of the stack using existing coinview.
        /// </summary>
        /// <param name="top">Coinview at the top of the stack.</param>
        public CoinViewStack(ICoinView top)
        {
            Guard.NotNull(top, nameof(top));

            this.Top = top;
            ICoinView current = top;
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
        public IEnumerable<ICoinView> GetElements()
        {
            ICoinView current = this.Top;
            while (current is IBackedCoinView)
            {
                yield return current;

                current = ((IBackedCoinView)current).Inner;
            }

            if (current != null)
                yield return current;
        }

        /// <summary>
        /// Finds a coinview of specific type in the stack.
        /// </summary>
        /// <typeparam name="T">Type of the coinview to search for.</typeparam>
        /// <returns>Coinview of the specific type from the stack or <c>null</c> if such a coinview is not in the stack.</returns>
        public T Find<T>()
        {
            ICoinView current = this.Top;
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
