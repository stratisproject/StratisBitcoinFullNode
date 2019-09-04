using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCJsonOutputFormatterTest
    {
        private JsonSerializerSettings settings;

        public RPCJsonOutputFormatterTest()
        {
            this.settings = new JsonSerializerSettings();
        }

        [Fact]
        public void CreateJsonSerializerCreatesSerializerWithProvidedSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Culture = new System.Globalization.CultureInfo("en-GB")
            };
            var formatter = new TestRPCJsonOutputFormatter(settings);
            JsonSerializer serializer = formatter.JsonSerializer;

            Assert.Equal("en-GB", serializer.Culture.Name);
        }

        [Fact]
        public void WriteResponseBodyAsyncWritesContextToResponseBody()
        {
            Stream bodyStream = new MemoryStream();
            DefaultHttpContext defaultContext = SetupDefaultContextWithResponseBodyStream(bodyStream);

            var context = new OutputFormatterWriteContext(defaultContext,
                (s, e) => new StreamWriter(s, e, 256, true), typeof(RPCAuthorization),
                new RPCAuthorization());

            var formatter = new RPCJsonOutputFormatter(this.settings);
            Task task = formatter.WriteResponseBodyAsync(context, Encoding.UTF8);
            task.Wait();
            
            using (var reader = new StreamReader(bodyStream))
            {
                bodyStream.Position = 0;
                JToken expected = JToken.Parse(@"{""result"":{""Authorized"":[],""AllowIp"":[]},""error"":null}");
                JToken actual = JToken.Parse(reader.ReadToEnd());
                actual.Should().BeEquivalentTo(expected);
            }
        }

        private static DefaultHttpContext SetupDefaultContextWithResponseBodyStream(Stream bodyStream)
        {
            var defaultContext = new DefaultHttpContext();
            var response = new HttpResponseFeature();
            response.Body = bodyStream;
            var featureCollection = new FeatureCollection();
            featureCollection.Set<IHttpResponseFeature>(response);
            defaultContext.Initialize(featureCollection);
            return defaultContext;
        }

        private class TestRPCJsonOutputFormatter : RPCJsonOutputFormatter
        {
            public TestRPCJsonOutputFormatter(JsonSerializerSettings serializerSettings) : base(serializerSettings)
            {
            }

            public new JsonSerializer JsonSerializer => base.JsonSerializer;
        }
    }
}
