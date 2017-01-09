using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Configuration
{
    public class DataFolder
    {
		public DataFolder(string path)
		{
			CoinViewPath = Path.Combine(path, "coinview");
			AddrManFile = Path.Combine(path, "addrman.dat");
			ChainPath = Path.Combine(path, "chain");
			BlockPath = Path.Combine(path, "blocks");
			RPCCookiePath = Path.Combine(path, ".cookie");
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
		public string RPCCookiePath
		{
			get;
			internal set;
		}
	}
}
