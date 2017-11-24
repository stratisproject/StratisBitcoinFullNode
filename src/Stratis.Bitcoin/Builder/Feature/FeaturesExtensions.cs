using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Builder.Feature
{
    /// <summary>
    /// Extensions to features collection.
    /// </summary>
    public static class FeaturesExtensions
    {
        /// <summary>
        /// Ensures a dependency feature type is present in the feature list.
        /// </summary>
        /// <typeparam name="T">The dependency feature type.</typeparam>
        /// <param name="features">List of features.</param>
        /// <returns>List of features.</returns>
        /// <exception cref="MissingDependencyException">Thrown if feature type is missing.</exception>
        public static IEnumerable<IFullNodeFeature> EnsureFeature<T>(this IEnumerable<IFullNodeFeature> features)
        {
            if (!features.OfType<T>().Any())
            {
                throw new MissingDependencyException($"Dependency feature {typeof(T)} cannot be found.");
            }

            return features;
        }
    }
}
