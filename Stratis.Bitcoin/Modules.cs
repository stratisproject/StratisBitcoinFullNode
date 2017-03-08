using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Stratis.Bitcoin
{
	public abstract class Module
	{
		public virtual int Priority { get; } = 50;
		public abstract void Configure(FullNode node, ServiceCollection serviceCollection);
		public abstract void Start(FullNode node, IServiceProvider service);
	}
}
