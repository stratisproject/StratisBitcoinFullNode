using System;
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
        private Action<ApiSettings> callback = null;

        /// <summary>
        /// Constructs this object.
        /// </summary>
        public ApiSettings()
        {
        }

        /// <summary>
        /// Constructs this object whilst providing a callback to override/constrain/extend 
        /// the settings provided by the Load method.
        /// </summary>
        /// <param name="callback">The callback used to override/constrain/extend the settings provided by the Load method.</param>
        public ApiSettings(Action<ApiSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        /// <summary>
        /// Loads the API related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public void Load(NodeSettings nodeSettings)
        {
            TextFileConfiguration config = nodeSettings.ConfigReader;

            var port = nodeSettings.Network.IsBitcoin() ? 37220 : 37221;

            this.ApiUri = config.GetOrDefault("apiuri", new Uri($"http://localhost:{port}"));

            this.callback?.Invoke(this);
        }
    }
}
