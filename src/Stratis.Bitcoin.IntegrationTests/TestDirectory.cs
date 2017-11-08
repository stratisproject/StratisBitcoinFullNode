using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class TestDirectory : IDisposable
    {
        public static TestDirectory Create([CallerMemberName]string name = null, bool clean = true)
        {
            var directory = new TestDirectory(Path.Combine("TestData", name), clean);
            directory.EnsureExists();
            return directory;
        }

        private void EnsureExists()
        {
            if (!Directory.Exists("TestData"))
                Directory.CreateDirectory("TestData");

            if (!Directory.Exists(this.FolderName))
                Directory.CreateDirectory(this.FolderName);
        }

        public TestDirectory(string name, bool clean)
        {
            this.FolderName = name;
            this.Clean = clean;
            if (this.Clean)
                CleanDirectory();
        }

        public string FolderName
        {
            get; set;
        }

        public bool Clean
        {
            get;
            private set;
        }
        private void CleanDirectory()
        {
            try
            {
                Directory.Delete(this.FolderName, true);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        public void Dispose()
        {
            if (this.Clean)
                CleanDirectory();
        }
    }
}