using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.PlatformAbstractions;

namespace Stratis.Bitcoin {
   /// <summary>
   /// A builder for <see cref="IFullNode"/>
   /// </summary>
   public class FullNodeBuilder : IFullNodeBuilder {
      private readonly List<Action<IServiceCollection>> _configureServicesDelegates;
      private readonly List<Action<ILoggerFactory>> _configureLoggingDelegates;

      private IConfiguration _config;
      private ILoggerFactory _loggerFactory;
      private FullNodeOptions _options;
      private bool _fullNodeBuilt;

      /// <summary>
      /// Initializes a new instance of the <see cref="FullNodeBuilder"/> class.
      /// </summary>
      public FullNodeBuilder() {
         _configureServicesDelegates = new List<Action<IServiceCollection>>();
         _configureLoggingDelegates = new List<Action<ILoggerFactory>>();

         _config = new ConfigurationBuilder()
             .AddEnvironmentVariables(prefix: "ASPNETCORE_")
             .Build();

         if (string.IsNullOrEmpty(GetSetting(FullNodeDefaults.EnvironmentKey))) {
            // Try adding legacy environment keys, never remove these.
            UseSetting(FullNodeDefaults.EnvironmentKey, Environment.GetEnvironmentVariable("Hosting:Environment")
                ?? Environment.GetEnvironmentVariable("ASPNET_ENV"));
         }
      }

      /// <summary>
      /// Add or replace a setting in the configuration.
      /// </summary>
      /// <param name="key">The key of the setting to add or replace.</param>
      /// <param name="value">The value of the setting to add or replace.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public IFullNodeBuilder UseSetting(string key, string value) {
         _config[key] = value;
         return this;
      }

      /// <summary>
      /// Get the setting value from the configuration.
      /// </summary>
      /// <param name="key">The key of the setting to look up.</param>
      /// <returns>The value the setting currently contains.</returns>
      public string GetSetting(string key) {
         return _config[key];
      }

      /// <summary>
      /// Specify the <see cref="ILoggerFactory"/> to be used by the web host.
      /// </summary>
      /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to be used.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public IFullNodeBuilder UseLoggerFactory(ILoggerFactory loggerFactory) {
         if (loggerFactory == null) {
            throw new ArgumentNullException(nameof(loggerFactory));
         }

         _loggerFactory = loggerFactory;
         return this;
      }

      /// <summary>
      /// Adds a delegate for configuring additional services for the host or web application. This may be called
      /// multiple times.
      /// </summary>
      /// <param name="configureServices">A delegate for configuring the <see cref="IServiceCollection"/>.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public IFullNodeBuilder ConfigureServices(Action<IServiceCollection> configureServices) {
         if (configureServices == null) {
            throw new ArgumentNullException(nameof(configureServices));
         }

         _configureServicesDelegates.Add(configureServices);
         return this;
      }

      /// <summary>
      /// Adds a delegate for configuring the provided <see cref="ILoggerFactory"/>. This may be called multiple times.
      /// </summary>
      /// <param name="configureLogging">The delegate that configures the <see cref="ILoggerFactory"/>.</param>
      /// <returns>The <see cref="IFullNodeBuilder"/>.</returns>
      public IFullNodeBuilder ConfigureLogging(Action<ILoggerFactory> configureLogging) {
         if (configureLogging == null) {
            throw new ArgumentNullException(nameof(configureLogging));
         }

         _configureLoggingDelegates.Add(configureLogging);
         return this;
      }

      /// <summary>
      /// Builds the required services and an <see cref="IFullNode"/> which hosts a web application.
      /// </summary>
      public IFullNode Build() {
         if (_fullNodeBuilt) {
            //ref. to use localized exceptions
            //throw new InvalidOperationException(Resources.WebHostBuilder_SingleInstance);
            throw new InvalidOperationException("full node already built");
         }
         _fullNodeBuilt = true;

         var fullNodeServices = BuildCommonServices();
         var applicationServices = fullNodeServices.Clone();
         var fullNodeServiceProvider = fullNodeServices.BuildServiceProvider();

         AddApplicationServices(applicationServices, fullNodeServiceProvider);

         var fullNode = new FullNode(
             applicationServices,
             fullNodeServiceProvider,
             _options,
             _config);

         fullNode.Initialize();

         return fullNode;
      }

      private IServiceCollection BuildCommonServices() {
         _options = new FullNodeOptions(_config);

         var services = new ServiceCollection();

         // The configured ILoggerFactory is added as a singleton here. AddLogging below will not add an additional one.
         if (_loggerFactory == null) {
            _loggerFactory = new LoggerFactory();
            services.AddSingleton(provider => _loggerFactory);
         }
         else {
            services.AddSingleton(_loggerFactory);
         }

         foreach (var configureLogging in _configureLoggingDelegates) {
            configureLogging(_loggerFactory);
         }

         //This is required to add ILogger of T.
         services.AddLogging();

         services.AddOptions();

         // Ensure object pooling is available everywhere.
         //services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

         foreach (var configureServices in _configureServicesDelegates) {
            configureServices(services);
         }

         return services;
      }


      private void AddApplicationServices(IServiceCollection services, IServiceProvider fullNodeServiceProvider) {
         // We are forwarding services from hosting contrainer so hosting container
         // can still manage their lifetime (disposal) shared instances with application services.
         // NOTE: This code overrides original services lifetime. Instances would always be singleton in
         // application container.
         var loggerFactory = fullNodeServiceProvider.GetService<ILoggerFactory>();
         services.Replace(ServiceDescriptor.Singleton(typeof(ILoggerFactory), loggerFactory));

         var listener = fullNodeServiceProvider.GetService<DiagnosticListener>();
         services.Replace(ServiceDescriptor.Singleton(typeof(DiagnosticListener), listener));
         services.Replace(ServiceDescriptor.Singleton(typeof(DiagnosticSource), listener));
      }
   }
}