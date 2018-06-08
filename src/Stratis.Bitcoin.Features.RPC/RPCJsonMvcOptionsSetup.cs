using System.Buffers;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCJsonMvcOptionsSetup : ConfigureOptions<MvcOptions>
    {
        /// <summary>
        /// Intiailizes a new instance of <see cref="T:Microsoft.AspNetCore.Mvc.Formatters.Json.Internal.MvcJsonMvcOptionsSetup" />.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="T:Microsoft.Extensions.Logging.ILoggerFactory" />.</param>
        /// <param name="jsonOptions"></param>
        /// <param name="charPool"></param>
        /// <param name="objectPoolProvider"></param>
        public RPCJsonMvcOptionsSetup(ILoggerFactory loggerFactory, IOptions<MvcJsonOptions> jsonOptions, ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider)
            : base(delegate (MvcOptions options)
            {
                ConfigureMvc(options, jsonOptions.Value.SerializerSettings, loggerFactory, charPool, objectPoolProvider);
            })
        {
        }

        public static void ConfigureMvc(MvcOptions options, JsonSerializerSettings serializerSettings, ILoggerFactory loggerFactory, ArrayPool<char> charPool, ObjectPoolProvider objectPoolProvider)
        {
            JsonOutputFormatter jsonOutput = options.OutputFormatters.OfType<JsonOutputFormatter>().First();
            options.OutputFormatters.Remove(jsonOutput);
            options.OutputFormatters.Add(new RPCJsonOutputFormatter(serializerSettings, charPool));
        }
    }
}
