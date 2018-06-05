using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.SidechainD
{
    internal class SimpleInitialBlockDownloadState : IInitialBlockDownloadState
    {
        public bool IsInitialBlockDownload()
        {
            return false;
        }
    }

    public static class FullNodeBuilderExtension
    {
        public static IFullNodeBuilder UseSimpleIBD(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default IBD implementation and replace it with the mock.
                        ServiceDescriptor ibdService = services.FirstOrDefault(x => x.ServiceType == typeof(IInitialBlockDownloadState));
                        services.AddSingleton<IInitialBlockDownloadState, SimpleInitialBlockDownloadState>();
                    });
                }
            });
            return fullNodeBuilder;
        }
    }
}
