using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Assumes that coins are coming in in the order that we want them, then adds coins until we reach the target.
    /// Inefficient, but deterministic.
    /// </summary>
    public class DeterministicCoinSelector : ICoinSelector
    {
        public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
        {
            var result = new List<ICoin>();
            IMoney total = Money.Zero;

            if (target.CompareTo(Money.Zero) == 0)
                return result;

            foreach (ICoin coin in coins)
            {
                // Build up our ongoing total
                total = total.Add(coin.Amount);
                result.Add(coin);

                // If we make the target, return the current set.
                if (total.CompareTo(target) >= 0)
                {
                    return result;
                }
            }

            // If we are here we didn't have enough funds.
            return null;
        }

    }
}
