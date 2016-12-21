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
		}

		public string CoinViewPath
		{
			get; set;
		}
	}
}
