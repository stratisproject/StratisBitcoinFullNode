using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNode : IDisposable
	{
		FullNodeServiceProvider Services { get; }

		void Start();
	}
}