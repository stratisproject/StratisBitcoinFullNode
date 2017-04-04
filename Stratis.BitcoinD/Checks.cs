using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;

namespace Stratis.BitcoinD
{
	public class Checks
	{
		public static bool VerifyAccess(NodeSettings nodeSettings) 
		{
			// Verify folders are accessible by attempting to create sub directories,
			// and removing them after the test.  Need to test write access.
			var dataFolder = new DataFolder(nodeSettings);
			foreach(var path in new []{ dataFolder.BlockPath, dataFolder.ChainPath, dataFolder.CoinViewPath }) 
			{
				try 
				{
				    var subDir = new System.IO.DirectoryInfo(path).CreateSubdirectory(Guid.NewGuid().ToString());
				    subDir.Delete();
				}
				catch(UnauthorizedAccessException ex)
				{
					Logs.Configuration.LogCritical(ex.Message);
					Console.ReadLine();
					return false;
				}
			}

			return true;
		}
	}
}
