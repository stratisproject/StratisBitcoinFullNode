using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Builder
{
	public interface IFullNode 
	{
		FullNodeServiceProvider Services { get; }

		void Start();
	}
}