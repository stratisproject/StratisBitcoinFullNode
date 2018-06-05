using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public class CoinDetails
    {
        public int Type { get; }
        public string Symbol { get; set; }
        public string Name { get; }

        public CoinDetails(string symbol, string name = null, int? type = null)
        {
            //TODO : Currently this is case sensitive...
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(string.Format("{0} can not be null, empty or whitespace", nameof(symbol)));
            if (KnownCoinsBySymbol.TryGetValue(symbol, out Tuple<string, int> coinDetails))
            {
                Type = coinDetails.Item2;
                Symbol = symbol;
                Name = coinDetails.Item1;
            }
            else if (string.IsNullOrEmpty(name) || !type.HasValue)
                throw new ArgumentNullException(string.Format("{0} is not a known coin and requires a valid name and type", symbol));

            Symbol = symbol;
            Name = name;
            Type = type.Value;
        }

        public static readonly ConcurrentDictionary<string, Tuple<string, int>> KnownCoinsBySymbol = 
            new ConcurrentDictionary<string, Tuple<string, int>>(
            new Dictionary<string, Tuple<string, int>>()
            {
                { "STRAT", new Tuple<string,int>("Stratis", 105) },
                { "TSTRAT", new Tuple<string,int>("TestStratis", 105) },
                { "BTC", new Tuple<string,int>("Bitcoin", 0) },
                { "TBTC", new Tuple<string,int>("TestBitcoin", 0) },
            });
    }
}