using System;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Configuration related to storage of transactions.
    /// </summary>
    public class ApiSettings
    {
        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }       

        private Action<ApiSettings> callback = null;

        public ApiSettings()
        {
        }

        public ApiSettings(Action<ApiSettings> callback)
            : this()
        {
            this.callback = callback;
        }

        public ApiSettings(NodeSettings nodeSettings, Action<ApiSettings> callback = null)
            : this(callback)
        {
            this.Load(nodeSettings);
        }

        /// <summary>
        /// Loads the storage related settings from the application configuration.
        /// </summary>
        /// <param name="nodeSettings">Application configuration.</param>
        public virtual void Load(NodeSettings nodeSettings)
        {
            var config = nodeSettings.ConfigReader;

            this.ApiUri = config.GetOrDefault("apiuri", new Uri(
                $"http://localhost:{ (nodeSettings.Network.ToString().StartsWith("Stratis") ? 37221 : 37220) }"));

            this.callback?.Invoke(this);
        }
    }
}
