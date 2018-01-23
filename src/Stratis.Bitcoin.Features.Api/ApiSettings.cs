using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Configuration related to the API interface.
    /// </summary>
    public class ApiSettings
    {
        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }

        /// <summary>The callback used to override/constrain/extend the settings provided by the Load method.</summary>
        private Action<ApiSettings> callback;

        /// <summary>
        /// Constructs this object whilst providing a callback to override/constrain/extend 
        /// the settings provided by the Load method.
        /// </summary>
        /// <param name="callback">The callback used to override/constrain/extend the settings provided by the Load method.</param>
        public ApiSettings(Action<ApiSettings> callback)
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the API related settings from the application configuration.
        /// </summary>
        public void Load(NodeSettings nodeSettings)
        {
            ILogger logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ApiSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            var port = nodeSettings.Network.IsBitcoin() ? 37220 : 37221;

            this.ApiUri = config.GetOrDefault("apiuri", new Uri($"http://localhost:{port}"));

            this.callback?.Invoke(this);

            logger.LogDebug("ApiUri is {0}.", this.ApiUri);
        }
    }
}
