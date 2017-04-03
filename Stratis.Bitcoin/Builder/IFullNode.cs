using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNode : IDisposable
	{
		IFullNodeServiceProvider Services { get; }

		void Start();
	}
}