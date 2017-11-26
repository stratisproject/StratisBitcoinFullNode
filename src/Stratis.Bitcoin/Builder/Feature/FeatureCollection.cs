using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Stratis.Bitcoin.Builder.Feature
{
    /// <summary>
    /// Defines methods for collection of features of the FullNode.
    /// </summary>
    public interface IFeatureCollection
    {
        /// <summary>List of features already registered in the collection.</summary>
        List<IFeatureRegistration> FeatureRegistrations { get; }

        /// <summary>Adds a new feature to the collection provided that the feature of the same type has not been added already.</summary>
        /// <typeparam name="TImplementation">Type of the feature to be added to the collection.</typeparam>
        /// <returns>Representation of the registered feature.</returns>
        IFeatureRegistration AddFeature<TImplementation>() where TImplementation : class, IFullNodeFeature;
    }

    /// <summary>
    /// Collection of features available to and/or used by the FullNode.
    /// </summary>
    public class FeatureCollection : IFeatureCollection
    {
        /// <summary>List of features already registered in the collection.</summary>
        private readonly List<IFeatureRegistration> featureRegistrations;

        /// <summary>Initializes the object instance.</summary>
        public FeatureCollection()
        {
            this.featureRegistrations = new List<IFeatureRegistration>();
        }

        /// <inheritdoc />
        public List<IFeatureRegistration> FeatureRegistrations
        {
            get
            {
                return this.featureRegistrations;
            }
        }

        /// <inheritdoc />
        public IFeatureRegistration AddFeature<TImplementation>() where TImplementation : class, IFullNodeFeature
        {
            if (this.featureRegistrations.Any(f => f.FeatureType == typeof(TImplementation)))
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Feature of type {0} has already been registered.", typeof(TImplementation).FullName));

            var featureRegistration = new FeatureRegistration<TImplementation>();
            this.featureRegistrations.Add(featureRegistration);

            return featureRegistration;
        }
    }
}
