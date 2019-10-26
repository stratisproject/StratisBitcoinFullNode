using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder
{
    /// <summary>
    /// Provider of access to services and features registered with the full node.
    /// </summary>
    public interface IFullNodeServiceProvider
    {
        /// <summary>List of registered features.</summary>
        IEnumerable<IFullNodeFeature> Features { get; }

        /// <summary>Provider to registered services.</summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Determines whether the service of the specified type T is registered.
        /// </summary>
        /// <typeparam name="T">A type to query against the service provider.</typeparam>
        /// <returns>
        ///   <c>true</c> if this instance is registered; otherwise, <c>false</c>.
        /// </returns>
        bool IsServiceRegistered<T>();

        /// <summary>
        /// Guard method that check whether the service of the specified type T is registered.
        /// If it doesn't exists, thrown an exception.
        /// </summary>
        /// <typeparam name="T">A type to query against the service provider.</typeparam>
        void EnsureServiceIsRegistered<T>();
    }

    /// <summary>
    /// Provider of access to services and features registered with the full node.
    /// </summary>
    public class FullNodeServiceProvider : IFullNodeServiceProvider
    {
        /// <summary>List of registered feature types.</summary>
        private readonly List<Type> featureTypes;

        /// <inheritdoc />
        public IEnumerable<IFullNodeFeature> Features
        {
            get
            {
                // features are enumerated in the same order
                // they where registered with the provider
                foreach (Type featureDescriptor in this.featureTypes)
                    yield return this.ServiceProvider.GetService(featureDescriptor) as IFullNodeFeature;
            }
        }

        /// <inheritdoc />
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Initializes a new instance of the object with service provider and list of registered feature types.
        /// </summary>
        /// <param name="serviceProvider">Provider to registered services.</param>
        /// <param name="featureTypes">List of registered feature types.</param>
        public FullNodeServiceProvider(IServiceProvider serviceProvider, List<Type> featureTypes)
        {
            Guard.NotNull(serviceProvider, nameof(serviceProvider));
            Guard.NotNull(featureTypes, nameof(featureTypes));

            this.ServiceProvider = serviceProvider;
            this.featureTypes = featureTypes;
        }

        /// <inheritdoc />
        public bool IsServiceRegistered<T>()
        {
            return this.ServiceProvider.GetService<T>() != null;
        }

        /// <inheritdoc />
        public void EnsureServiceIsRegistered<T>()
        {
            if (!this.IsServiceRegistered<T>())
                throw new MissingServiceException(typeof(T));
        }
    }
}
