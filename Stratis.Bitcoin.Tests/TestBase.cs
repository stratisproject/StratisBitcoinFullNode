using Stratis.Bitcoin.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests
{
    public class TestBase
    {
        public static DataFolder AssureEmptyDirAsDataFolder(string dir)
        {            
            var dataFolder = new DataFolder(new NodeSettings { DataDir = AssureEmptyDir(dir) });
            return dataFolder;
        }

        public static string AssureEmptyDir(string dir)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            return dir;
        }
    }
}
