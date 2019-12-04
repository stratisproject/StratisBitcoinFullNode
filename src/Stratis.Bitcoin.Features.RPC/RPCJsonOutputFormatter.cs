using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public interface IRPCJsonOutputFormatter
    {
        Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding);
    }

    public class RPCJsonOutputFormatter : TextOutputFormatter, IRPCJsonOutputFormatter
    {
        public static readonly MediaTypeHeaderValue ApplicationJson = MediaTypeHeaderValue.Parse("application/json").CopyAsReadOnly();

        public static readonly MediaTypeHeaderValue TextJson = MediaTypeHeaderValue.Parse("text/json").CopyAsReadOnly();

        public static readonly MediaTypeHeaderValue ApplicationJsonPatch = MediaTypeHeaderValue.Parse("application/json-patch+json").CopyAsReadOnly();

        private JsonSerializer serializer;

        /// <summary>
        /// Used during serialization get <see cref="T:Newtonsoft.Json.JsonSerializer" />.
        /// </summary>
        protected virtual JsonSerializer JsonSerializer
        {
            get
            {
                if (this.serializer == null)
                    this.serializer = JsonSerializer.Create(this.SerializerSettings);

                return this.serializer;
            }
        }

        /// <summary>
        /// Gets the <see cref="T:Newtonsoft.Json.JsonSerializerSettings" /> used to configure the <see cref="T:Newtonsoft.Json.JsonSerializer" />.
        /// </summary>
        /// <remarks>
        /// Any modifications to the <see cref="T:Newtonsoft.Json.JsonSerializerSettings" /> object after this
        /// <see cref="T:Microsoft.AspNetCore.Mvc.Formatters.JsonOutputFormatter" /> has been used will have no effect.
        /// </remarks>
        protected JsonSerializerSettings SerializerSettings { get; set; }

        /// <summary>
        /// Initializes a new <see cref="T:Microsoft.AspNetCore.Mvc.Formatters.JsonOutputFormatter" /> instance.
        /// </summary>
        /// <param name="serializerSettings">
        /// The <see cref="T:Newtonsoft.Json.JsonSerializerSettings" />. Should be either the application-wide settings
        /// (<see cref="P:Microsoft.AspNetCore.Mvc.MvcJsonOptions.SerializerSettings" />) or an instance
        /// <see cref="M:Microsoft.AspNetCore.Mvc.Formatters.JsonSerializerSettingsProvider.CreateSerializerSettings" /> initially returned.
        /// </param>        
        /// <returns>The <see cref="T:Newtonsoft.Json.JsonSerializer" /> used during serialization and deserialization.</returns>
        public RPCJsonOutputFormatter(JsonSerializerSettings serializerSettings)
        {
            Guard.NotNull(serializerSettings, nameof(serializerSettings));

            this.SerializerSettings = serializerSettings;
            this.SupportedEncodings.Add(Encoding.UTF8);
            this.SupportedEncodings.Add(Encoding.Unicode);
            this.SupportedMediaTypes.Add(ApplicationJson);
            this.SupportedMediaTypes.Add(TextJson);
        }

        /// <inheritdoc />
        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(selectedEncoding, nameof(selectedEncoding));

            //{"result":null,"error":{"code":-32601,"message":"Method not found"},"id":1}
            JToken jsonResult = JToken.FromObject(context.Object, this.JsonSerializer);
            JObject response = new JObject
            {
                ["result"] = jsonResult,
                ["error"] = null
            };
            
            using (TextWriter textWriter = context.WriterFactory(context.HttpContext.Response.Body, selectedEncoding))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(textWriter))
            {
                await response.WriteToAsync(jsonWriter);

                // Perf: call FlushAsync to call WriteAsync on the stream with any content left in the TextWriter's
                // buffers. This is better than just letting dispose handle it (which would result in a synchronous
                // write).
                await textWriter.FlushAsync();
            }
        }
    }
}
