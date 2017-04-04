using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;
using System.Globalization;

namespace Stratis.Bitcoin.Builder.Feature
{
	public interface IFeatureCollection
	{
		List<IFeatureRegistration> FeatureRegistrations { get; }

		IFeatureRegistration AddFeature<TImplementation>() where TImplementation : class, IFullNodeFeature;
	}

	public class FeatureCollection : IFeatureCollection
	{
		private readonly List<IFeatureRegistration> featureRegistrations;

		public FeatureCollection()
		{
			this.featureRegistrations = new List<IFeatureRegistration>();
		}

		public List<IFeatureRegistration> FeatureRegistrations
		{
			get
			{
				return this.featureRegistrations;
			}			
		}

		public IFeatureRegistration AddFeature<TImplementation>() where TImplementation : class, IFullNodeFeature
		{
			if (featureRegistrations.Any(f => f.FeatureType == typeof(TImplementation)))
				throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "Feature of type {0} has already been registered.", typeof(TImplementation).FullName));			

			var featureRegistration = new FeatureRegistration<TImplementation>();
			this.featureRegistrations.Add(featureRegistration);

			return featureRegistration;
		}
	}
}