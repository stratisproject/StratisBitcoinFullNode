﻿using System;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder.Feature
{
    /// <summary>
    /// Tests checking for feature dependencies.
    /// </summary>
    public class FeaturesDependencyCheckingTest
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

            /// <inheritdoc />
            public void ValidateDependencies(IFullNodeServiceProvider services)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// A mock feature.
        /// </summary>
        private class FeatureB : FeatureA
        {
        }

        #endregion Mock Features

        #region Tests

        /// <summary>
        /// Test no exceptions fired when checking features that exist.
        /// </summary>
        [Fact]
        public void DependencyCheckWithValidDependencies()
        {
            var builder = new FullNodeBuilder().UseNodeSettings(NodeSettings.Default());

            builder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FeatureB>();
            });

            builder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FeatureA>()
                    .DependOn<FeatureB>();
            });

            builder.Build();
        }

        /// <summary>
        /// Test that missing feature throws exception.
        /// </summary>
        [Fact]
        public void DependencyCheckWithInvalidDependenciesThrowsException()
        {
            var builder = new FullNodeBuilder().UseNodeSettings(NodeSettings.Default());
            builder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<FeatureA>()
                    .DependOn<FeatureB>();
            });

            Assert.Throws<MissingDependencyException>(() =>
            {
                builder.Build();
            });
        }

        #endregion Tests
    }
}
