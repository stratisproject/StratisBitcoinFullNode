using System.Collections.Generic;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.RPC.Converters;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.Tests.Converters
{
    public class BtcDecimalJsonConverterTest
    {
        private readonly ITestOutputHelper _console;

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new BtcDecimalJsonConverter()
            }
        };

        public BtcDecimalJsonConverterTest(ITestOutputHelper console)
        {
            //use this if you want to see the console output for xunit tests.
            //because xunit runs tests in parallel.
            this._console = console;
        }

        [Fact]
        public void CanNotReadJson()
        {
            var converter = new BtcDecimalJsonConverter();
            Assert.False(converter.CanRead, "because the converter is only used to convert TO json.");
        }

        [Fact]
        public void CanConvertDecimals()
        {
            var converter = new BtcDecimalJsonConverter();
            Assert.True(converter.CanConvert(typeof(decimal)), "because the converter is used to convert decimals to BTC (8 digit place) decimal");
        }

        [Fact]
        public void WhenConvertingObjectToJsonDecimalsArePaddedWithUpToEightZeros()
        {
            var input = new TestClassForConverter
            {
                Amount = 1.0m
            };
            var result = JsonConvert.SerializeObject(input, this._jsonSerializerSettings);
            this._console.WriteLine(result);
            Assert.Equal("{\"Amount\":1.00000000}", result);
        }

        [Fact]
        public void WhenConvertingObjectToJsonAndThereAreMoreThanEightPlaces()
        {
            var input = new TestClassForConverter
            {
                Amount = 1.123456789m
            };
            var result = JsonConvert.SerializeObject(input, this._jsonSerializerSettings);
            this._console.WriteLine(result);
            Assert.Equal("{\"Amount\":1.123456789}", result);  //is this correct?
        }

        [Fact]
        public void WhenConvertingObjectToJsonDecimalsArePaddedWithUpToEightZerosForFullValues()
        {
            var input = new TestClassForConverter
            {
                Amount = 9m
            };
            var result = JsonConvert.SerializeObject(input, this._jsonSerializerSettings);
            this._console.WriteLine(result);
            Assert.Equal("{\"Amount\":9.00000000}", result);
        }
    }

    public class TestClassForConverter
    {

        [JsonConverter(typeof(BtcDecimalJsonConverter))]
        public decimal Amount { get; set; }

        public override string ToString()
        {
            return $"{nameof(Amount)}: {Amount}";
        }
    }
}