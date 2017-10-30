using System;
using System.Collections.Generic;
using System.Text;

namespace NBitcoin.Indexer
{
	public class IndexerConfigurationErrorsException : Exception
	{
		public IndexerConfigurationErrorsException(string message) : base(message)
		{

		}
	}
}
