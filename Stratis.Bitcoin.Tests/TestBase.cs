using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using System.IO;

namespace Stratis.Bitcoin.Tests
{
    public class TestBase
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes logger factory for inherited tests.
        /// </summary>
        public TestBase()
        {
            this.loggerFactory = new LoggerFactory();
        }

        public static DataFolder AssureEmptyDirAsDataFolder(string dir)
        {
            var dataFolder = new DataFolder(new NodeSettings { DataDir = AssureEmptyDir(dir) });
            return dataFolder;
        }

        public static string AssureEmptyDir(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);

            return dir;
        }
    }
}