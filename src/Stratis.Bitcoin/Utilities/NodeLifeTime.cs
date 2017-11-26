using System;
using System.Threading;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Allows consumers to perform cleanup during a graceful shutdown.
    /// </summary>
    public interface INodeLifetime
    {
        /// <summary>
        /// Triggered when the application host has fully started and is about to wait
        /// for a graceful shutdown.
        /// </summary>
        CancellationToken ApplicationStarted { get; }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// Requests may still be in flight. Shutdown will block until this event completes.
        /// </summary>
        CancellationToken ApplicationStopping { get; }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// All requests should be complete at this point. Shutdown will block
        /// until this event completes.
        /// </summary>
        CancellationToken ApplicationStopped { get; }

        /// <summary>Requests termination the current application.</summary>
        void StopApplication();
    }

    /// <summary>
    /// Allows consumers to perform cleanup during a graceful shutdown.
    /// Borrowed from asp.net core
    /// </summary>
    public class NodeLifetime : INodeLifetime
    {
        private readonly CancellationTokenSource startedSource = new CancellationTokenSource();

        private readonly CancellationTokenSource stoppingSource = new CancellationTokenSource();

        private readonly CancellationTokenSource stoppedSource = new CancellationTokenSource();

        /// <summary>
        /// Triggered when the application host has fully started and is about to wait
        /// for a graceful shutdown.
        /// </summary>
        public CancellationToken ApplicationStarted
        {
            get
            {
                return this.startedSource.Token;
            }
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// Request may still be in flight. Shutdown will block until this event completes.
        /// </summary>
        public CancellationToken ApplicationStopping
        {
            get
            {
                return this.stoppingSource.Token;
            }
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// All requests should be complete at this point. Shutdown will block
        /// until this event completes.
        /// </summary>
        public CancellationToken ApplicationStopped
        {
            get
            {
                return this.stoppedSource.Token;
            }
        }

        /// <summary>
        /// Signals the ApplicationStopping event and blocks until it completes.
        /// </summary>
        public void StopApplication()
        {
            CancellationTokenSource stoppingSource = this.stoppingSource;
            bool lockTaken = false;
            try
            {
                Monitor.Enter((object)stoppingSource, ref lockTaken);
                try
                {
                    this.stoppingSource.Cancel(false);
                }
                catch (Exception)
                {
                }
            }
            finally
            {
                if (lockTaken)
                    Monitor.Exit((object)stoppingSource);
            }
        }

        /// <summary>
        /// Signals the ApplicationStarted event and blocks until it completes.
        /// </summary>
        public void NotifyStarted()
        {
            try
            {
                this.startedSource.Cancel(false);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Signals the ApplicationStopped event and blocks until it completes.
        /// </summary>
        public void NotifyStopped()
        {
            try
            {
                this.stoppedSource.Cancel(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
