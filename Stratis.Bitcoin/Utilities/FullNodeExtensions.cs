﻿using System;
using System.Reflection;
#if !NOASSEMBLYCONTEXT
using System.Runtime.Loader;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Common;
using Stratis.Bitcoin.Common.Hosting;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Extension methods for IFullNode interface.
    /// </summary>
    public static class FullNodeExtensions
    {
        /// <summary>
        /// Installs handlers for graceful shutdown in the console, starts a full node and waits until it terminates. 
        /// </summary>
        /// <param name="node">Full node to run.</param>
        public static void Run(this IFullNode node)
        {
            var done = new ManualResetEventSlim(false);
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Action shutdown = () =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        Console.WriteLine("Application is shutting down...");
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException exception)
                        {
                            Console.WriteLine(exception.Message);
                        }
                    }

                    done.Wait();
                };
#if !NOASSEMBLYCONTEXT
                var assemblyLoadContext = AssemblyLoadContext.GetLoadContext(typeof(FullNode).GetTypeInfo().Assembly);
                assemblyLoadContext.Unloading += context => shutdown();
#endif
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    shutdown();
                    // Don't terminate the process immediately, wait for the Main thread to exit gracefully.
                    eventArgs.Cancel = true;
                };

                node.Run(cts.Token, "Application started. Press Ctrl+C to shut down.");
                done.Set();
            }
        }

        /// <summary>
        /// Starts a full node, sets up cancellation tokens for its shutdown, and waits until it terminates. 
        /// </summary>
        /// <param name="node">Full node to run.</param>
        /// <param name="cancellationToken">Cancellation token that triggers when the node should be shut down.</param>
        /// <param name="shutdownMessage">Message to display on the console to instruct the user on how to invoke the shutdown.</param>
        public static void Run(this IFullNode node, CancellationToken cancellationToken, string shutdownMessage)
        {
            using (node)
            {
                node.Start();

                if (!string.IsNullOrEmpty(shutdownMessage))
                {
                    Console.WriteLine();
                    Console.WriteLine(shutdownMessage);
                    Console.WriteLine();
                }

                cancellationToken.Register(state =>
                {
                    ((INodeLifetime)state).StopApplication();
                },
                node.NodeLifetime);

                var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                node.NodeLifetime.ApplicationStopping.Register(obj =>
                {
                    var tcs = (TaskCompletionSource<object>)obj;
                    tcs.TrySetResult(null);
                }, waitForStop);

                //await waitForStop.Task;
                waitForStop.Task.GetAwaiter().GetResult();

                node.Stop();
            }
        }
    }
}