using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Builder.Feature
{
	public class FeatureCollection
	{
		public readonly List<FeatureRegistration> FeatureRegistrations;

		public FeatureCollection()
		{
			FeatureRegistrations = new List<FeatureRegistration>();
		}

		public FeatureRegistration AddFeature<TImplementation>() where TImplementation : class, IFullNodeFeature
		{
			Guard.Assert(FeatureRegistrations.All(f => f.FeatureType != typeof(TImplementation)));
			var featureRegistration = new FeatureRegistration(typeof(TImplementation));

			FeatureRegistrations.Add(featureRegistration);
			return featureRegistration;
		}
	}
}