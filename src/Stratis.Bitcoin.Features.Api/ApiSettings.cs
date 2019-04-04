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
        /// <summary>The default api host.</summary>
        public const string DefaultApiHost = "http://localhost";

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>URI to node's API interface.</summary>
        public Uri ApiUri { get; set; }

        /// <summary>Port of node's API interface.</summary>
        public int ApiPort { get; set; }

        /// <summary>URI to node's API interface.</summary>
        public Timer KeepaliveTimer { get; private set; }

        // TODO: Refactor static PrintHelp method.
        private static ApiSettings settingsHelp;

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
        /// <param name="defaultArgs">Allows overriding the default values before config and command line are applied.</param>
        public ApiSettings(NodeSettings nodeSettings, Action<ApiSettings> defaultArgs = null)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(ApiSettings).FullName);

            // Constants.
            this.UseHttps = false;
            this.HttpsCertificateFilePath = (string)null;
            this.ApiPort = nodeSettings.Network.DefaultAPIPort;
            this.ApiUri = new Uri($"{DefaultApiHost}");
            this.KeepaliveTimer = null;

            // DefaultArgs.
            defaultArgs?.Invoke(this);

            // For printing help.
            // TODO: Refactor.
            settingsHelp = (ApiSettings)this.MemberwiseClone();

            // Config and Command Line.
            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.UseHttps = config.GetOrDefault("usehttps", this.UseHttps);
            this.HttpsCertificateFilePath = config.GetOrDefault("certificatefilepath", this.HttpsCertificateFilePath);

            if (this.UseHttps && string.IsNullOrWhiteSpace(this.HttpsCertificateFilePath))
                throw new ConfigurationException("The path to a certificate needs to be provided when using https. Please use the argument 'certificatefilepath' to provide it.");

            string defaultApiHost = this.ApiUri.IsDefaultPort ? this.ApiUri.OriginalString : this.ApiUri.OriginalString.Split(':')[0];

            if (this.UseHttps)
                defaultApiHost = defaultApiHost.Replace(@"http://", @"https://");
            else
                defaultApiHost = defaultApiHost.Replace(@"https://", @"http://");

            string apiHost = config.GetOrDefault("apiuri", defaultApiHost, this.logger);
            var apiUri = new Uri(apiHost);

            // Find out which port should be used for the API.
            int apiPort = config.GetOrDefault("apiport", this.ApiPort, this.logger);

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
            double keepAlive = config.GetOrDefault("keepalive", (double?)null, this.logger) * 1000 ?? this.KeepaliveTimer?.Interval ?? 0;
            if (this.KeepaliveTimer != null)
            {
                if (keepAlive > 0)
                {
                    this.KeepaliveTimer.Interval = keepAlive;
                }
                else
                {
                    this.KeepaliveTimer.Dispose();
                    this.KeepaliveTimer = null;
                }
            }
            else
            {
                if (keepAlive > 0)
                {
                    this.KeepaliveTimer = new Timer
                    {
                        AutoReset = false,
                        Interval = keepAlive
                    };
                }
            }
        }

        /// <summary>Prints the help information on how to configure the API settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-apiuri=<string>                  URI to node's API interface. Defaults to '{ settingsHelp.ApiUri }'.");
            builder.AppendLine($"-apiport=<0-65535>                Port of node's API interface. Defaults to { settingsHelp.ApiPort }.");
            builder.AppendLine($"-keepalive=<seconds>              Keep Alive interval (set in seconds). Default: 0 (no keep alive).");
            builder.AppendLine($"-usehttps=<bool>                  Use https protocol on the API. Defaults to false.");
            builder.AppendLine($"-certificatefilepath=<string>     Path to the certificate used for https traffic encryption. Defaults to <null>. Password protected files are not supported. On MacOs, only p12 certificates can be used without password.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####API Settings####");
            builder.AppendLine($"#URI to node's API interface. Defaults to '{ settingsHelp.ApiUri }'.");
            builder.AppendLine($"#apiuri={ settingsHelp.ApiUri }");
            builder.AppendLine($"#Port of node's API interface. Defaults to { settingsHelp.ApiPort }.");
            builder.AppendLine($"#apiport={ settingsHelp.ApiPort }");
            builder.AppendLine($"#Keep Alive interval (set in seconds). Default: 0 (no keep alive).");
            builder.AppendLine($"#keepalive=0");
            builder.AppendLine($"#Use HTTPS protocol on the API. Default is false.");
            builder.AppendLine($"#usehttps=false");
            builder.AppendLine($"#Path to the file containing the certificate to use for https traffic encryption. Password protected files are not supported. On MacOs, only p12 certificates can be used without password.");
            builder.AppendLine(@"#Please refer to .Net Core documentation for usage: 'https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509certificate2.-ctor?view=netcore-2.1#System_Security_Cryptography_X509Certificates_X509Certificate2__ctor_System_Byte___'.");
            builder.AppendLine($"#certificatefilepath=");
        }
    }
}
