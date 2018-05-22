using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NBitcoin.Tests
{
    public class UnspentCoinTests
    {
        [Fact]
        public void CanDecodeUnspentCoinWatchOnlyAddress()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210,
    ""spendable"" : false
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            Assert.Equal("test label", unspentCoin.Account);
            Assert.False(unspentCoin.IsSpendable);
            Assert.Null(unspentCoin.RedeemScript);
        }

        [Fact]
        public void CanDecodeUnspentCoinLegacyPre_0_10_0()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            // Versions prior to 0.10.0 were always spendable (but had no JSON field).
            Assert.True(unspentCoin.IsSpendable);
        }

        [Fact]
        public void CanDecodeUnspentCoinWithRedeemScript()
        {
            var testJson =
@"{
    ""txid"" : ""d54994ece1d11b19785c7248868696250ab195605b469632b7bd68130e880c9a"",
    ""vout"" : 1,
    ""address"" : ""mgnucj8nYqdrPFh2JfZSB1NmUThUGnmsqe"",
    ""account"" : ""test label"",
    ""scriptPubKey"" : ""76a9140dfc8bafc8419853b34d5e072ad37d1a5159f58488ac"",
    ""redeemScript"" : ""522103310188e911026cf18c3ce274e0ebb5f95b007f230d8cb7d09879d96dbeab1aff210243930746e6ed6552e03359db521b088134652905bd2d1541fa9124303a41e95621029e03a901b85534ff1e92c43c74431f7ce72046060fcf7a95c37e148f78c7725553ae"",
    ""amount"" : 0.00010000,
    ""confirmations"" : 6210,
    ""spendable"" : true
}";
            var testData = JObject.Parse(testJson);
            var unspentCoin = new UnspentCoin(testData, Network.TestNet);

            Console.WriteLine("Redeem Script: {0}", unspentCoin.RedeemScript);
            Assert.NotNull(unspentCoin.RedeemScript);
        }
    }
}
