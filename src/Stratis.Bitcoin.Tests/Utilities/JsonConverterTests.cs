using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.OpenAsset;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class JsonConverterTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanSerializeInJson()
        {
            var k = new Key();
            CanSerializeInJsonCore(DateTimeOffset.UtcNow);
            CanSerializeInJsonCore(new byte[] { 1, 2, 3 });
            CanSerializeInJsonCore(k);
            CanSerializeInJsonCore(Money.Coins(5.0m));
            CanSerializeInJsonCore(k.PubKey.GetAddress(Network.Main));
            CanSerializeInJsonCore(new KeyPath("1/2"));
            CanSerializeInJsonCore(Network.Main);
            CanSerializeInJsonCore(new uint256(RandomUtils.GetBytes(32)));
            CanSerializeInJsonCore(new uint160(RandomUtils.GetBytes(20)));
            CanSerializeInJsonCore(new AssetId(k.PubKey));
            CanSerializeInJsonCore(k.PubKey.ScriptPubKey);
            CanSerializeInJsonCore(new Key().PubKey.WitHash.GetAddress(Network.Main));
            CanSerializeInJsonCore(new Key().PubKey.WitHash.ScriptPubKey.GetWitScriptAddress(Network.Main));
            ECDSASignature sig = k.Sign(new uint256(RandomUtils.GetBytes(32)));
            CanSerializeInJsonCore(sig);
            CanSerializeInJsonCore(new TransactionSignature(sig, SigHash.All));
            CanSerializeInJsonCore(k.PubKey.Hash);
            CanSerializeInJsonCore(k.PubKey.ScriptPubKey.Hash);
            CanSerializeInJsonCore(k.PubKey.WitHash);
            CanSerializeInJsonCore(k);
            CanSerializeInJsonCore(k.PubKey);
            CanSerializeInJsonCore(new WitScript(new Script(Op.GetPushOp(sig.ToDER()), Op.GetPushOp(sig.ToDER()))));
            CanSerializeInJsonCore(new LockTime(1));
            CanSerializeInJsonCore(new LockTime(DateTime.UtcNow));
        }

        [Fact]
        public void CanSerializeRandomClass()
        {
            string str = Serializer.ToString(new DummyClass() { ExtPubKey = new ExtKey().Neuter().GetWif(Network.RegTest) }, Network.RegTest);
            Assert.NotNull(Serializer.ToObject<DummyClass>(str, Network.RegTest));
        }

        private T CanSerializeInJsonCore<T>(T value)
        {
            string str = Serializer.ToString(value);
            T obj2 = Serializer.ToObject<T>(str, Network.Main);
            Assert.Equal(str, Serializer.ToString(obj2));
            return obj2;
        }
    }

    public class DummyClass
    {
        public BitcoinExtPubKey ExtPubKey
        {
            get; set;
        }
    }
}