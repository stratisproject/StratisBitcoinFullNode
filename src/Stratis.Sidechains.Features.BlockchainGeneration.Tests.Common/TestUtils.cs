using System.IO;
using System.Threading;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common
{
    public static class TestUtils
    {
        public static void ShellCleanupFolder(string testFolderPath, int timeout = 30000)
        {
            if (!Directory.Exists(testFolderPath)) return;

            using (var fw = new FileSystemWatcher(Path.GetDirectoryName(testFolderPath)))
            using (var mre = new ManualResetEventSlim())
            {
                fw.EnableRaisingEvents = true;
                fw.Deleted += (s, e) =>
                {
                    mre.Set();
                };
                Directory.Delete(testFolderPath, true);
                mre.Wait(timeout);
            }
        }
    }
}
