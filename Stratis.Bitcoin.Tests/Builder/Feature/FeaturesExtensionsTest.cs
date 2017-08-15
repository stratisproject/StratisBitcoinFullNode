using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder.Feature
{
    /// <summary>
    /// Tests the features extensions.
    /// </summary>
    public class FeaturesExtensionsTest
    {
        #region Mock Features

        /// <summary>
        /// A mock feature.
        /// </summary>
        private class FeatureA : IFullNodeFeature
        {
            /// <inheritdoc />
            public void Start()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public void Stop()
            {
                throw new NotImplementedException();
            }

            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// A mock feature.
        /// </summary>
        private class FeatureB : IFullNodeFeature
        {
            /// <inheritdoc />
            public void Start()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public void Stop()
            {
                throw new NotImplementedException();
            }

            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region Tests

        /// <summary>
        /// Test no exceptions fired when checking features that exist.
        /// </summary>
        [Fact]
        public void EnsureFeatureWithValidDependencies()
        {
            List<IFullNodeFeature> features = new List<IFullNodeFeature>();
            features.Add(new FeatureA());
            features.Add(new FeatureB());

            features.EnsureFeature<FeatureA>();
            features.EnsureFeature<FeatureB>();
        }

        /// <summary>
        /// Test that missing feature throws exception.
        /// </summary>
        [Fact]
        public void EnsureFeatureWithInvalidDependenciesThrowsException()
        {
            List<IFullNodeFeature> features = new List<IFullNodeFeature>();
            features.Add(new FeatureA());

            features.EnsureFeature<FeatureA>();
            Assert.Throws<MissingDependencyException>(() => features.EnsureFeature<FeatureB>());           
        }

        #endregion
    }
}
