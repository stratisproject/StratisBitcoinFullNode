using System.IO;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Class that prevents another instance of the node to run in the same data folder
    /// and allows external applications to see if the node is running.
    /// </summary>
    public class NodeRunningLock
    {
        private readonly string lockFileName;

        private FileStream fileStream;

        public NodeRunningLock(DataFolder dataFolder)
        {
            this.lockFileName = Path.Combine(dataFolder.RootPath, "lockfile");
        }

        public bool TryLockNodeFolder()
        {
            try
            {
                this.fileStream = new FileStream(this.lockFileName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public void UnlockNodeFolder()
        {
            this.fileStream.Close();
            File.Delete(this.lockFileName);
        }
    }
}
