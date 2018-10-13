using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.OpenAsset;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class JsonConverterTests
    {
        private readonly Network networkMain;
        private readonly Network networkRegTest;

        public JsonConverterTests()
        {
            this.networkMain = new BitcoinMain();
            this.networkRegTest = new BitcoinRegTest();
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanSerializeInJson()
        {
            var k = new Key();
            CanSerializeInJsonCore(DateTimeOffset.UtcNow);
            CanSerializeInJsonCore(new byte[] { 1, 2, 3 });
            CanSerializeInJsonCore(k);
            CanSerializeInJsonCore(Money.Coins(5.0m));
            CanSerializeInJsonCore(k.PubKey.GetAddress(this.networkRegTest));
            CanSerializeInJsonCore(new KeyPath("1/2"));
            CanSerializeInJsonCore(this.networkRegTest);
            CanSerializeInJsonCore(new uint256(RandomUtils.GetBytes(32)));
            CanSerializeInJsonCore(new uint160(RandomUtils.GetBytes(20)));
            CanSerializeInJsonCore(new AssetId(k.PubKey));
            CanSerializeInJsonCore(k.PubKey.ScriptPubKey);
            CanSerializeInJsonCore(new Key().PubKey.WitHash.GetAddress(this.networkRegTest));
            CanSerializeInJsonCore(new Key().PubKey.WitHash.ScriptPubKey.GetWitScriptAddress(this.networkRegTest));
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
            string jsonString = Serializer.ToString(new DummyClass() { ExtPubKey = new ExtKey().Neuter().GetWif(this.networkRegTest) }, this.networkRegTest);
            Assert.NotNull(Serializer.ToObject<DummyClass>(jsonString, this.networkRegTest));
        }

        private T CanSerializeInJsonCore<T>(T value)
        {
            string jsonString = Serializer.ToString(value, this.networkRegTest);
            T deserializedObject = Serializer.ToObject<T>(jsonString, this.networkRegTest);

            Assert.Equal(jsonString, Serializer.ToString(deserializedObject, this.networkRegTest));

            return deserializedObject;
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