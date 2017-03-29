using System;
using Stratis.Bitcoin.Configuration;

namespace Stratis.BitcoinD
{
	public class Checks
	{
		public static void VerifyAccess(NodeSettings nodeSettings) 
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
				catch(UnauthorizedAccessException)
				{
				    throw new UnauthorizedAccessException(string.Format("Access to the path '{0}' was denied.", path));
				}
			}
		}
	}
}
