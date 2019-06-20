using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Coin selector that will use all the coins its given.
    /// </summary>
    public class ConsolidationCoinSelector : ICoinSelector
    {
        public IEnumerable<ICoin> Select(IEnumerable<ICoin> coins, IMoney target)
        {
            return coins;
        }
    }
}
