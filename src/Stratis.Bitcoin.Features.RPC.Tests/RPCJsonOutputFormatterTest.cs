using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Xunit;

namespace Stratis.Bitcoin.Features.RPC.Tests
{
    public class RPCJsonOutputFormatterTest
    {
        private TestRPCJsonOutputFormatter formatter;
        private JsonSerializerSettings settings;
        private ArrayPool<char> charpool;

        public RPCJsonOutputFormatterTest()
        {
            this.settings = new JsonSerializerSettings();
            this.charpool = ArrayPool<char>.Create();

            this.formatter = new TestRPCJsonOutputFormatter(this.settings, this.charpool);
        }

        [Fact]
        public void CreateJsonWriterCreatesNewJsonWriterWithTextWriter()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (TextWriter writer = new StreamWriter(memoryStream))
                {
                    using (var reader = new StreamReader(memoryStream))
                    {
                        JsonWriter result = this.formatter.CreateJsonWriter(writer);
                        result.WriteStartObject();
                        result.WriteEndObject();

                        writer.Flush();
                        memoryStream.Position = 0;
                        Assert.Equal("{}", reader.ReadToEnd());
                    }
                }
            }
        }

        [Fact]
        public void CreateJsonSerializerCreatesSerializerWithProvidedSettings()
        {
            this.settings.Culture = new System.Globalization.CultureInfo("en-GB");
            this.formatter = new TestRPCJsonOutputFormatter(this.settings, this.charpool);

            JsonSerializer serializer = this.formatter.CreateJsonSerializer();

            Assert.Equal("en-GB", serializer.Culture.Name);
        }

        [Fact]
        public void WriteObjectWritesObjectToWriter()
        {
            using (var memoryStream = new MemoryStream())
            {
                using (TextWriter writer = new StreamWriter(memoryStream))
                {
                    this.formatter.WriteObject(writer, new RPCAuthorization());
                    using (var reader = new StreamReader(memoryStream))
                    {
                        writer.Flush();
                        memoryStream.Position = 0;
                        Assert.Equal("{\"Authorized\":[],\"AllowIp\":[]}", reader.ReadToEnd());
                    }
                }
            }
        }

        [Fact]
        public void WriteResponseBodyAsyncWritesContextToResponseBody()
        {
            Stream bodyStream = new MemoryStream();
            DefaultHttpContext defaultContext = SetupDefaultContextWithResponseBodyStream(bodyStream);

            Stream stream = null;
            var context = new OutputFormatterWriteContext(defaultContext,
                (s, e) =>
                {
                    if (stream == null)
                    {
                        // only capture first stream. bodyStream is already under the test's control.
                        stream = s;
                    }

                    return new StreamWriter(s, e, 256, true);
                }, typeof(RPCAuthorization),
                new RPCAuthorization());

            Task task = this.formatter.WriteResponseBodyAsync(context, Encoding.UTF8);
            task.Wait();

            using (var reader = new StreamReader(stream))
            {
                stream.Position = 0;
                string result = reader.ReadToEnd();
                Assert.Equal("{\"Authorized\":[],\"AllowIp\":[]}", result);
            }

            using (var reader = new StreamReader(bodyStream))
            {
                bodyStream.Position = 0;
                string result = reader.ReadToEnd();
                Assert.Equal("{\"result\":{\"Authorized\":[],\"AllowIp\":[]},\"id\":1,\"error\":null}", result);
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
            public TestRPCJsonOutputFormatter(JsonSerializerSettings serializerSettings, ArrayPool<char> charPool) : base(serializerSettings, charPool)
            {
            }

            public new JsonWriter CreateJsonWriter(TextWriter writer)
            {
                return base.CreateJsonWriter(writer);
            }

            public new JsonSerializer CreateJsonSerializer()
            {
                return base.CreateJsonSerializer();
            }
        }
    }
}
