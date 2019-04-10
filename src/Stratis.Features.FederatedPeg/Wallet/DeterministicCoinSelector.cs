using System.Collections.Generic;
using System.Linq;
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

            // TODO: This can be simpler, remove GroupBy
            var orderedCoinGroups = coins.GroupBy(c => new Key().ScriptPubKey)
                .Select(scriptPubKeyCoins => new
                {
                    Amount = scriptPubKeyCoins.Select(c => c.Amount).Sum(Money.Zero),
                    Coins = scriptPubKeyCoins.ToList()
                });

            foreach (var coinGroup in orderedCoinGroups)
            {
                // Build up our ongoing total
                total = total.Add(coinGroup.Amount);
                result.AddRange(coinGroup.Coins);

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
