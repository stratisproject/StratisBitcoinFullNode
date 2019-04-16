using System;
using System.Text;
using System.Timers;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Configuration related to the API interface.
    /// </summary>
    public class ApiSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }

        /// <summary>Port of node's API interface.</summary>
        public int ApiPort { get; set; }

        /// <summary>URI to node's API interface.</summary>
        public Timer KeepaliveTimer { get; private set; }

        /// <summary>
        /// The HTTPS certificate file path.
        /// </summary>
        /// <remarks>
        /// Password protected certificates are not supported. On MacOs, only p12 certificates can be used without password.
        /// Please refer to .Net Core documentation for usage: <seealso cref="https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate2.-ctor?view=netcore-2.1#System_Security_Cryptography_X509Certificates_X509Certificate2__ctor_System_Byte___" />.
        /// </remarks>
        public string HttpsCertificateFilePath { get; set; }

        /// <summary>Use HTTPS or not.</summary>
        public bool UseHttps { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        /// <param name="defaults">The default values.</param>
        public ApiSettings(NodeSettings nodeSettings, ApiSettingsDefaults defaults = null)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ApiSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            defaults = defaults ?? new ApiSettingsDefaults(nodeSettings.Network);

            this.UseHttps = config.GetOrDefault("usehttps", defaults.UseHttps);
            this.HttpsCertificateFilePath = config.GetOrDefault("certificatefilepath", defaults.HttpsCertificateFilePath);

            if (this.UseHttps && string.IsNullOrWhiteSpace(this.HttpsCertificateFilePath))
                throw new ConfigurationException("The path to a certificate needs to be provided when using https. Please use the argument 'certificatefilepath' to provide it.");

            var defaultApiHost = this.UseHttps
                ? defaults.ApiHost.Replace(@"http://", @"https://")
                : defaults.ApiHost.Replace(@"https://", @"http://");

            string apiHost = config.GetOrDefault("apiuri", defaultApiHost, this.logger);
            var apiUri = new Uri(apiHost);

            // Find out which port should be used for the API.
            int apiPort = config.GetOrDefault("apiport", defaults.ApiPort, this.logger);

            // If no port is set in the API URI.
            if (apiUri.IsDefaultPort)
            {
                this.ApiUri = new Uri($"{apiHost}:{apiPort}");
                this.ApiPort = apiPort;
            }
            // If a port is set in the -apiuri, it takes precedence over the default port or the port passed in -apiport.
            else
            {
                this.ApiUri = apiUri;
                this.ApiPort = apiUri.Port;
            }

            // Set the keepalive interval (set in seconds).
            int keepAlive = config.GetOrDefault("keepalive", defaults.KeepAlive, this.logger);
            if (keepAlive > 0)
            {
                this.KeepaliveTimer = new Timer
                {
                    AutoReset = false,
                    Interval = keepAlive * 1000
                };
            }
        }

        /// <summary>Prints the help information on how to configure the API settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();
            var defaults = new ApiSettingsDefaults(network);

            builder.AppendLine($"-apiuri=<string>                  URI to node's API interface. Defaults to '{ defaults.ApiHost }'.");
            builder.AppendLine($"-apiport=<0-65535>                Port of node's API interface. Defaults to { defaults.ApiPort }.");
            builder.AppendLine($"-keepalive=<seconds>              Keep Alive interval (set in seconds). Default: { defaults.KeepAlive } (0 = no keep alive).");
            builder.AppendLine($"-usehttps=<bool>                  Use https protocol on the API. Defaults to { defaults.KeepAlive }.");
            builder.AppendLine($"-certificatefilepath=<string>     Path to the certificate used for https traffic encryption. Defaults to { (defaults.HttpsCertificateFilePath ?? "<null>") }.");
            builder.AppendLine($"                                  Password protected files are not supported. On MacOs, only p12 certificates can be used without password.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            var defaults = new ApiSettingsDefaults(network);

            builder.AppendLine("####API Settings####");
            builder.AppendLine($"#URI to node's API interface. Defaults to '{ defaults.ApiHost }'.");
            builder.AppendLine($"#apiuri={ defaults.ApiHost }");
            builder.AppendLine($"#Port of node's API interface. Defaults to { defaults.ApiPort }.");
            builder.AppendLine($"#apiport={ defaults.ApiPort }");
            builder.AppendLine($"#Keep Alive interval (set in seconds). Default: { defaults.KeepAlive } (0 = no keep alive).");
            builder.AppendLine($"#keepalive={ defaults.KeepAlive }");
            builder.AppendLine($"#Use HTTPS protocol on the API. Default is { defaults.UseHttps }.");
            builder.AppendLine($"#usehttps={ defaults.UseHttps }");
            builder.AppendLine($"#Path to the file containing the certificate to use for https traffic encryption. Password protected files are not supported. On MacOs, only p12 certificates can be used without password.");
            builder.AppendLine(@"#Please refer to .Net Core documentation for usage: 'https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate2.-ctor?view=netcore-2.1#System_Security_Cryptography_X509Certificates_X509Certificate2__ctor_System_Byte___'.");
            builder.AppendLine($"#certificatefilepath={ defaults.HttpsCertificateFilePath }");
        }
    }
}
