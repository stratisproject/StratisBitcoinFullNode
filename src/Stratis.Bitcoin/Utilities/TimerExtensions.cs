using System.Timers;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for the <see cref="Timer"/> class.
    /// </summary>
    public static class TimerExtensions
    {
        /// <summary>
        /// Reset a timer from the start.
        /// </summary>
        /// <param name="timer">The timer to reset.</param>
        public static void Reset(this Timer timer)
        {
            timer.Stop();
            timer.Start();
        }
    }
}
