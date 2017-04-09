using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Tests
{
    public class FullNodeBaseTest : TestBase
    {
        protected FeatureCollection featureCollection;
        protected List<Action<IFeatureCollection>> featureCollectionDelegates;
        protected FullNodeBuilder fullNodeBuilder;
        protected List<Action<IServiceCollection>> serviceCollectionDelegates;
        protected List<Action<IServiceProvider>> serviceProviderDelegates;

        public FullNodeBaseTest()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection);
        }

    }
}
