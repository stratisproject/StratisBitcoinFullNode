using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Based on the DefaultCoinSelector but with randomness removed, and GroupByScriptKey set to false.
    /// </summary>
    public class DeterministicCoinSelector : ICoinSelector
    {
        public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
        {
            IMoney zero = target.Sub(target);

            var result = new List<ICoin>();
            IMoney total = zero;

            if (target.CompareTo(zero) == 0)
                return result;


            var orderedCoinGroups = coins.GroupBy(c => new Key().ScriptPubKey)
                .Select(scriptPubKeyCoins => new
                {
                    Amount = scriptPubKeyCoins.Select(c => c.Amount).Sum(zero),
                    Coins = scriptPubKeyCoins.ToList()
                }).OrderBy(c => c.Amount);


            var targetCoin = orderedCoinGroups
                            .FirstOrDefault(c => c.Amount.CompareTo(target) == 0);
            //If any of your UTXO² matches the Target¹ it will be used.
            if (targetCoin != null)
                return targetCoin.Coins;

            foreach (var coinGroup in orderedCoinGroups)
            {
                // If this UTXO is greater than the target, just use it.
                if (coinGroup.Amount.CompareTo(target) == 1)
                {
                    return coinGroup.Coins;
                }

                // Build up our ongoing total
                total = total.Add(coinGroup.Amount);
                result.AddRange(coinGroup.Coins);

                // If we go over the total, return the current set.
                if (total.CompareTo(target) == 1)
                {
                    return result;
                }
            }

            // If we didn't have enough funds, return null
            if (total.CompareTo(target) == -1)
                return null;
            return result;
        }

    }
}
