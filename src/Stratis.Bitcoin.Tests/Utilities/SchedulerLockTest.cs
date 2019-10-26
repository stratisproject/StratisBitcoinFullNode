using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class SchedulerLockTest
    {
        // TODO: write test scenarios for the AsyncLock
        // example when a exclusive delegate breaks in to a concurrent delegate
        // and back the two parts of the exclusive delegate need to be called sequentially

        public static bool IsSequential(int[] a)
        {
            return Enumerable.Range(1, a.Length - 1).All(i => a[i] - 1 == a[i - 1]);
        }

        [Fact]
        public void SchedulerPairSessionTest()
        {
            var session = new SchedulerLock();
            var collector = new List<int>();

            Task task = Task.Run(async () =>
            {
                await await session.WriteAsync(async () =>
                {
                    collector.Add(1);
                    // push another exclusive task to the scheduler
                    Task exclusiveTask = session.WriteAsync(() => collector.Add(2));
                    // await a concurrent task, this will split the current method in two tasks
                    // the pushed exclusive task will processes before the await yields back control
                    await session.ReadAsync(() => collector.Add(3));
                    collector.Add(4);
                    await exclusiveTask;
                });
            });

            task.Wait();

            Assert.True(IsSequential(collector.ToArray()));
        }
    }
}
