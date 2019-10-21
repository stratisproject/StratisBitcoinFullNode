using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Features.SQLiteWalletRepository.Tests
{
    public class DBLockTests
    {
        [Fact]
        public void LocksCanLockAndReleaseOverMultipleThreads()
        {
            var instance = new int[1000];
            var locks = new DBLock[100];
            for (int i = 0; i < locks.Length; i++)
                locks[i] = new DBLock();

            Parallel.ForEach(instance.Select((_, n) => n), new ParallelOptions() { MaxDegreeOfParallelism = 20 }, (n) =>
            {
                var dbLock = locks[n / 10];

                if (dbLock.Wait() && dbLock.Wait())
                {
                    dbLock.Release();
                    dbLock.Release();
                }
            });
        }
    }
}
