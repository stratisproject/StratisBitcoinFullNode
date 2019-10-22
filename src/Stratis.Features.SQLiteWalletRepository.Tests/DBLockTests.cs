using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Stratis.Features.SQLiteWalletRepository.Tests
{
    public class DBLockTests
    {
        [Fact]
        public void LocksCanLockAndReleaseOverMultipleThreads()
        {
            const int threadsPerLock = 10;

            var gotlock = new bool[1000];
            var locks = new DBLock[gotlock.Length / threadsPerLock];
            for (int i = 0; i < locks.Length; i++)
                locks[i] = new DBLock();
            var lockObj = new object();

            Parallel.ForEach(gotlock.Select((_, n) => n), new ParallelOptions() { MaxDegreeOfParallelism = gotlock.Length }, (n) =>
            {
                var dbLock = locks[n / threadsPerLock];

                // Odd numbered threads release locks once they have been taken by the corresponding even numbered thread.
                if ((n & 1) == 1)
                {
                    while (!gotlock[n & ~1])
                        Thread.Yield();

                    Thread.Sleep(10);

                    gotlock[n & ~1] = false;
                    dbLock.Release();
                }
                else
                {
                    dbLock.Wait();
                    gotlock[n] = true;

                }
            });
        }
    }
}
