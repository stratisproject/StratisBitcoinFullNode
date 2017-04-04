using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNodeServiceProvider
	{
		IEnumerable<IFullNodeFeature> Features { get; }
		IServiceProvider ServiceProvider { get; }
	}

	public class FullNodeServiceProvider : IFullNodeServiceProvider
	{
		private readonly List<Type> featureTypes;

		public FullNodeServiceProvider(IServiceProvider serviceProvider, List<Type> featureTypes)
		{
			Guard.NotNull(serviceProvider, nameof(serviceProvider));
			Guard.NotNull(featureTypes, nameof(featureTypes));

			this.ServiceProvider = serviceProvider;
			this.featureTypes = featureTypes;
		}

		public IServiceProvider ServiceProvider { get; }

		public IEnumerable<IFullNodeFeature> Features
		{
			get
			{
				// features are enumerated in the same order 
				// they where registered with the provider

				foreach (var featureDescriptor in this.featureTypes)
					yield return this.ServiceProvider.GetService(featureDescriptor) as IFullNodeFeature;
			}
		}
	}
}
