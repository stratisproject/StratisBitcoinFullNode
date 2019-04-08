using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Tests.Models
{
    public class TransactionModelsTest : BaseModelTest, IDisposable
    {
        private const string TxBlock10Hex = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0704ffff001d0136ffffffff0100f2052a01000000434104fcc2888ca91cf0103d8c5797c256bf976e81f280205d002d85b9b622ed1a6f820866c7b5fe12285cfa78c035355d752fc94a398b67597dc4fbb5b386816425ddac00000000";
        private const string TxBlock10Hash = "d3ad39fa52a89997ac7381c95eeffeaf40b66af7a57e9eba144be0a175a12b11";
        private TransactionBriefModel txBlock10CoinbaseModelBrief;

        private const string TxBlock460373CoinbaseHex = "01000000010000000000000000000000000000000000000000000000000000000000000000ffffffff1e0355060704eba7e3582f4254432e434f4d2fb6000ddbcbe5000000000000ffffffff02a0ede0500000000017a914e083685a1097ce1ea9e91987ab9e94eae33d8a13870000000000000000266a24aa21a9ede6c8871b5b06ac4f8735c0bff5ebe3ca53d030e153a2735710535c78f2e71fdc00000000";
        private const string TxBlock460373CoinbaseHash = "ed6cadd7028e1406b766a57b14998f47ce5c44ec594ce7c26a6c5eab98f2e8dd";
        private TransactionVerboseModel txBlock460373CoinbaseModelVerbose;

        private const string TxTwoInTwoOutHex = "0100000002b1a7582dad5e18960e7a8c0f3d9193fbb30f0c47b8114202eee31074f23bd0190e0000006b483045022100a050938989d6204761e473dedcaf61c029aca04a8c6d8d8c2d7c4357b23bc877022011ef04f87f9d90ce44421f44d0f4307949780ad5706701bdd8e350a5c31a3138012102077645ad77f8231f022d8801e956897f0e6a241f9be6b1ffde9ff5c7953d7af9ffffffffa9165ba655be36d78f4cd9522189cdf5dc4fb9fb796bb058c200c403930da2da000000006b483045022100919c8102e45f0ae9095b8052aa1888d616d72f2ec40cf54defd84d9fab22bda802207a724bacd9e9a0cf47a1f74e294dd597d849cc2a129b8add98957d37a721cddf012103c161ca4ac16fba4cca3f506acafb97e01cb55e95ea672054f2af566e2a35c8b0ffffffff02c4311723000000001976a91469d0a322604dcb268163764b9b476e33ee366cbc88ac28896310000000001976a914034978ca44c994ee7a80e1de11f2371809fac48f88ac00000000";
        private const string TxTwoInTwoOutHash = "951f379563ab5631dd2d249f06e76fe0ad19c03caabb22db2352f47ddc51fc31";
        private TransactionVerboseModel txTwoInTwoOutModelVerbose;

        public TransactionModelsTest()
        {
            var network = KnownNetworks.Main;
            this.txBlock10CoinbaseModelBrief = new TransactionBriefModel(network.CreateTransaction(TxBlock10Hex));
            this.txBlock460373CoinbaseModelVerbose = new TransactionVerboseModel(network.CreateTransaction(TxBlock460373CoinbaseHex), network);
            this.txTwoInTwoOutModelVerbose = new TransactionVerboseModel(network.CreateTransaction(TxTwoInTwoOutHex), network);
        }

        public void Dispose()
        {
            this.txBlock10CoinbaseModelBrief = null;
            this.txBlock460373CoinbaseModelVerbose = null;
            this.txTwoInTwoOutModelVerbose = null;
        }

        [Fact]
        public void TransactionModelBriefRenderTest()
        {
            TransactionBriefModel model = this.txBlock10CoinbaseModelBrief;
            string json = ModelToJson(model);

            string expectedJson = "\"" + TxBlock10Hex + "\"";

            Assert.Equal(expectedJson, json);
        }

        [Fact]
        public void TransactionModelVerboseRenderTest()
        {
            var expectedPropertyNameOrder = new string[] { "hex", "txid", "hash", "version", "size", "vsize", "locktime", "vin", "vout" };
            JObject obj = ModelToJObject(this.txBlock460373CoinbaseModelVerbose);
            Assert.True(obj.HasValues);

            int actualElements = obj.Children().Count();
            string actualHex = obj.Value<string>("hex");
            string actualTxid = obj.Value<string>("txid");
            string actualHash = obj.Value<string>("hash");
            int? actualSize = obj.Value<int>("size");
            int? actualVSize = obj.Value<int>("vsize");
            var actualVersion = obj.Value<int?>("version");
            var actualLocktime = obj.Value<int?>("locktime");
            IEnumerable<string> actualPropertyNameOrder = obj.Children().Select(o => (o as JProperty)?.Name);

            Assert.Equal(9, actualElements);
            Assert.Equal(TxBlock460373CoinbaseHex, actualHex);
            Assert.Equal(TxBlock460373CoinbaseHash, actualTxid);
            Assert.Equal(TxBlock460373CoinbaseHash, actualHash);
            Assert.Equal(160, actualSize);
            Assert.Equal(160, actualVSize);
            Assert.Equal(1, actualVersion);
            Assert.Equal(0, actualLocktime);
            Assert.Equal(expectedPropertyNameOrder, actualPropertyNameOrder);
        }

        [Fact]
        public void TransactionModelVerboseRenderCoinbaseTest()
        {
            var expectedPropertyNameOrder = new string[] { "coinbase", "sequence" };
            string expectedCoinbase = "0355060704eba7e3582f4254432e434f4d2fb6000ddbcbe5000000000000";
            JObject obj = ModelToJObject(this.txBlock460373CoinbaseModelVerbose);
            Assert.True(obj.HasValues);
            JToken vin = obj["vin"];
            Assert.NotNull(vin);

            int actualVinCount = vin.Count();
            int? actualVinInnerElements = vin.FirstOrDefault()?.Count();
            string actualCoinbase = vin.FirstOrDefault()?.Value<string>("coinbase");
            var actualSequence = vin.FirstOrDefault()?.Value<uint>("sequence");
            IEnumerable<string> actualPropertyNameOrder = vin.FirstOrDefault()?.Select(o => (o as JProperty)?.Name);

            Assert.Equal(1, actualVinCount);
            Assert.Equal(2, actualVinInnerElements);
            Assert.Equal(4294967295, actualSequence);
            Assert.Equal(expectedCoinbase, actualCoinbase);
            Assert.Equal(expectedPropertyNameOrder, actualPropertyNameOrder);
        }

        [Fact]
        public void TransactionModelVerboseRenderVoutTest()
        {
            var expectedPropertyNameOrder = new string[] { "value", "n", "scriptPubKey" };
            JObject obj = ModelToJObject(this.txBlock460373CoinbaseModelVerbose);
            Assert.True(obj.HasValues);
            JToken vout = obj["vout"];
            Assert.NotNull(vout);

            int actualVoutCount = vout.Count();
            int? actualVoutInnerElements = vout.FirstOrDefault()?.Count();
            decimal? actualTotalValue = vout.Sum(o => o.Value<decimal?>("value"));
            var actualLastVoutN = vout.LastOrDefault().Value<int?>("n");
            IEnumerable<string> actualPropertyNameOrder = vout.FirstOrDefault()?.Select(o => (o as JProperty)?.Name);

            Assert.Equal(2, actualVoutCount);
            Assert.Equal(3, actualVoutInnerElements);
            Assert.Equal(13.56918176m, actualTotalValue);
            Assert.Equal(actualVoutCount - 1, actualLastVoutN);
            Assert.Equal(expectedPropertyNameOrder, actualPropertyNameOrder);
        }

        [Fact]
        public void TransactionModelVerboseRenderVoutScriptTest()
        {
            var expectedFirstPropertyNameOrder = new string[] { "asm", "hex", "reqSigs", "type", "addresses" };
            var expectedSecondPropertyNameOrder = new string[] { "asm", "hex", "type" };
            JObject obj = ModelToJObject(this.txBlock460373CoinbaseModelVerbose);
            var firstScript = obj["vout"]?.FirstOrDefault()?.Value<JToken>("scriptPubKey");
            var secondScript = obj["vout"]?.LastOrDefault()?.Value<JToken>("scriptPubKey");

            string actualFirstVoutScriptType = firstScript?.Value<string>("type");
            var actualReqSigs = firstScript?.Value<int>("reqSigs");
            int? actualAddressCount = firstScript?["addresses"]?.Count();
            string actualSecondVoutScriptType = secondScript?.Value<string>("type");
            IEnumerable<string> actualFirstPropertyNameOrder = firstScript?.Select(o => (o as JProperty)?.Name);
            IEnumerable<string> actualSecondPropertyNameOrder = secondScript?.Select(o => (o as JProperty)?.Name);

            Assert.Equal("scripthash", actualFirstVoutScriptType);
            Assert.Equal(1, actualReqSigs);
            Assert.Equal(1, actualAddressCount);
            Assert.Equal("nulldata", actualSecondVoutScriptType);
            Assert.Equal(expectedFirstPropertyNameOrder, actualFirstPropertyNameOrder);
            Assert.Equal(expectedSecondPropertyNameOrder, actualSecondPropertyNameOrder);
        }

        [Fact]
        public void TransactionModelVerboseRenderNonCoinbaseTest()
        {
            var expectedPropertyNameOrder = new string[] { "txid", "vout", "scriptSig", "sequence" };
            JObject obj = ModelToJObject(this.txTwoInTwoOutModelVerbose);
            Assert.True(obj.HasValues);
            JToken vin = obj["vin"];
            JToken firstIn = vin.FirstOrDefault();
            JToken lastIn = vin.LastOrDefault();

            int actualVinCount = vin.Count();
            string actualFirstTxId = firstIn?.Value<string>("txid");
            var actualFirstNdx = firstIn?.Value<int?>("vout");
            var actualFirstSequence = firstIn?.Value<uint>("sequence");
            string actualLastTxId = lastIn?.Value<string>("txid");
            var actualLastNdx = lastIn?.Value<int?>("vout");
            var actualLastSequence = lastIn?.Value<uint>("sequence");
            IEnumerable<string> actualPropertyNameOrder = firstIn?.Select(o => (o as JProperty)?.Name);

            Assert.Equal(2, actualVinCount);
            Assert.Equal("19d03bf27410e3ee024211b8470c0fb3fb93913d0f8c7a0e96185ead2d58a7b1", actualFirstTxId);
            Assert.Equal(14, actualFirstNdx);
            Assert.Equal(4294967295, actualFirstSequence);
            Assert.Equal("daa20d9303c400c258b06b79fbb94fdcf5cd892152d94c8fd736be55a65b16a9", actualLastTxId);
            Assert.Equal(0, actualLastNdx);
            Assert.Equal(4294967295, actualLastSequence);
            Assert.Equal(expectedPropertyNameOrder, actualPropertyNameOrder);
        }
    }

    public class BaseModelTest
    {
        protected static JObject ModelToJObject(object model)
        {
            string json = ModelToJson(model);
            JObject obj = JObject.Parse(json);
            return obj;
        }

        protected static string ModelToJson(object model)
        {
            var formatter = new RPCJsonOutputFormatter(new JsonSerializerSettings(), System.Buffers.ArrayPool<char>.Create());
            var sw = new StringWriter();
            formatter.WriteObject(sw, model);
            string json = sw.ToString();
            return json;
        }
    }
}