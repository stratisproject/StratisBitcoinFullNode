using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
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
            public bool InitializeBeforeBase { get; set; }

            public void LoadConfiguration()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public Task InitializeAsync()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public void Dispose()
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
            public bool InitializeBeforeBase { get; set; }

            public void LoadConfiguration()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public Task InitializeAsync()
            {
                throw new NotImplementedException();
            }

            /// <inheritdoc />
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }

        #endregion Mock Features

        #region Tests

        /// <summary>
        /// Test no exceptions fired when checking features that exist.
        /// </summary>
        [Fact]
        public void EnsureFeatureWithValidDependencies()
        {
            var features = new List<IFullNodeFeature>();
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
            var features = new List<IFullNodeFeature>();
            features.Add(new FeatureA());

            features.EnsureFeature<FeatureA>();
            Assert.Throws<MissingDependencyException>(() => features.EnsureFeature<FeatureB>());
        }

        #endregion Tests
    }
}
