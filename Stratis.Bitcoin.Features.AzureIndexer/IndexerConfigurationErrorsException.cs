using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.AzureIndexer
{
	public class IndexerConfigurationErrorsException : Exception
	{
		public IndexerConfigurationErrorsException(string message) : base(message)
		{

		}
	}
}
