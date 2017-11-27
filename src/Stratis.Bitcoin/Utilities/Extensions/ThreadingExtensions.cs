using System.Threading;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    public static class ThreadingExtensions
    {
        /// <summary>
        /// Don't throw SemaphoreFullException
        /// https://stackoverflow.com/questions/4706734/semaphore-what-is-the-use-of-initial-count
        /// </summary>
        public static void SafeRelease(this SemaphoreSlim me)
        {
            try
            {
                me.SafeRelease(1);
            }
            catch (SemaphoreFullException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Don't throw SemaphoreFullException
        /// https://stackoverflow.com/questions/4706734/semaphore-what-is-the-use-of-initial-count
        /// </summary>
        public static void SafeRelease(this SemaphoreSlim me, int releaseCount)
        {
            try
            {
                me.Release(releaseCount);
            }
            catch (SemaphoreFullException)
            {
                // ignore
            }
        }
    }
}
