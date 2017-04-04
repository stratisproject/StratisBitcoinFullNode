using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Configuration
{
	/// <summary>
	/// Contains path locations to folders and files on disk.
	/// Used by various components of the full node.
	/// </summary>
    public class DataFolder
    {
		// Note: a location name should described if its a file or a folder
		// File - location name end with "File" (i.e AddrMan[File])
		// Folder - location name end with "Path" (i.e CoinView[Path])
		public DataFolder(NodeSettings settings)
		{
			string path = settings.DataDir;
			CoinViewPath = Path.Combine(path, "coinview");
			AddrManFile = Path.Combine(path, "addrman.dat");
			ChainPath = Path.Combine(path, "chain");
			BlockPath = Path.Combine(path, "blocks");
			RPCCookieFile = Path.Combine(path, ".cookie");
		}

		public string AddrManFile
		{
			get;
			set;
		}
		public string CoinViewPath
		{
			get; set;
		}
		public string ChainPath
		{
			get;
			internal set;
		}
		public string BlockPath
		{
			get;
			internal set;
		}
		public string RPCCookieFile
		{
			get;
			internal set;
		}
	}
}
