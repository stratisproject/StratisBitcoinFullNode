using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Sidechains.Features.BlockchainGeneration
{
    public class CoinDetails
    {
        public int Type { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        
        public CoinDetails(int coinType, string networkName)
        {
            if (networkName == null || !KnownCoinsByCoinType.TryGetValue(coinType, out var coinNamesAndSymbols)) return;
            var isTestNetwork = networkName.ToLower().Contains("test");
            var nameAndSymbol = isTestNetwork
                ? coinNamesAndSymbols.FirstOrDefault(ns => ns.Name.ToLower().Contains("test"))
                : coinNamesAndSymbols.FirstOrDefault(ns => !ns.Name.ToLower().Contains("test"));
            Type = coinType;
            Symbol = nameAndSymbol.Symbol;
            Name = nameAndSymbol.Name;
        }

        public static readonly ConcurrentDictionary<int, List<(string Name, string Symbol)>> KnownCoinsByCoinType =
            new ConcurrentDictionary<int, List<(string Name, string Symbol)>>(
                new Dictionary<int, List<(string Name, string Symbol)>>()
                {
                    { 105, new List<(string Name, string Symbol)>{ ("Stratis", "STRAT"), ("TestStratis", "TSTRAT")} },
                    { 0, new List<(string Name, string Symbol)>{ ("Bitcoin", "BTC"), ("TestBitcoin", "TBTC") }},
                    { 3000, new List<(string Name, string Symbol)>{ ("Apex", "APEX") }},
                    { 3001, new List<(string Name, string Symbol)>{ ("TestApex", "TAPEX") }},
                });
    }


}