using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of <see cref="AsyncExecutionEvent{TSender,TArg}"/> class.
    /// </summary>
    public class AsyncExecutionEventTest
    {
        /// <summary>
        /// Checks that registered callback is executed when <see cref="AsyncExecutionEvent{TSender, TArg}.ExecuteCallbacksAsync(TSender, TArg)"/>
        /// is called and then if the callback is unregistered, it is no longer called. It also checks that the arguments passed to the callback 
        /// are correct.
        /// </summary>
        [Fact]
        public async void AsyncExecutionEvent_OnlyRegisteredCallbackIsCalled_Async()
        {
            using (var executionEvent = new AsyncExecutionEvent<int, int>())
            {
                int value = 1;
                bool called = false;
                int senderValue = 2;

                AsyncExecutionEventCallback<int, int> callbackAsync = (sender, arg) =>
                {
                    called = true;
                    Assert.Equal(value, arg);
                    Assert.Equal(senderValue, sender);
                    return Task.CompletedTask;
                };
                executionEvent.Register(callbackAsync);

                await executionEvent.ExecuteCallbacksAsync(senderValue, value);
                Assert.True(called);

                // Now unregister callback and check that it won't be called anymore.
                called = false;
                executionEvent.Unregister(callbackAsync);
                await executionEvent.ExecuteCallbacksAsync(senderValue + 1, value + 1);
                Assert.False(called);
            }
        }

        /// <summary>
        /// Checks that the guarantee given by <see cref="AsyncExecutionEvent{TSender, TArg}.Unregister(AsyncExecutionEventCallback{TSender, TArg})"/> holds,
        /// i.e. that the callback is never called after it has been unregistered.
        /// </summary>
        [Fact]
        public void AsyncExecutionEvent_UnregisterGuarantee()
        {
            var rnd = new Random();
            using (var executionEvent = new AsyncExecutionEvent<object, bool>())
            {
                for (int i = 0; i < 100; i++)
                {
                    bool unregistered = false;
                    bool badCall = false;
                    AsyncExecutionEventCallback<object, bool> callbackAsync = (sender, arg) =>
                    {
                        // badCall should never be set to true here 
                        // because unregistered is only set to true
                        // after Unregister has been called.
                        badCall = arg;
                        return Task.CompletedTask;
                    };

                    executionEvent.Register(callbackAsync);

                    Task unregisterTask = Task.Run(async () =>
                    {
                        // In about half of the cases, we yield execution here.
                        // So that we randomize the order of the two tasks.
                        if (rnd.Next(100) >= 50) await Task.Yield();
                        executionEvent.Unregister(callbackAsync);
                        unregistered = true;
                    });

                    Task callTask = Task.Run(async () =>
                    {
                        // Random delay to help randomize the order of two tasks.
                        await Task.Delay(rnd.Next(10));
                        await executionEvent.ExecuteCallbacksAsync(null, unregistered);
                    });

                    Task.WaitAll(unregisterTask, callTask);
                    Assert.False(badCall);
                }
            }
        }

        /// <summary>
        /// Checks that the same callback can't be registered twice.
        /// </summary>
        [Fact]
        public void AsyncExecutionEvent_CantRegisterTwice()
        {
            using (var executionEvent = new AsyncExecutionEvent<object, bool>())
            {
                AsyncExecutionEventCallback<object, bool> callback1Async = (sender, arg) =>
                {
                    return Task.CompletedTask;
                };

                AsyncExecutionEventCallback<object, bool> callback2Async = (sender, arg) =>
                {
                    return Task.CompletedTask;
                };

                executionEvent.Register(callback1Async);
                
                // We can register a second callback with same body, but it is different function.
                executionEvent.Register(callback2Async);

                // But we can't register a same function twice.
                Assert.Throws<ArgumentException>(() => executionEvent.Register(callback1Async));

                executionEvent.Unregister(callback1Async);
                executionEvent.Unregister(callback2Async);
            }
        }

        /// <summary>
        /// Checks that an attempt to unregister a callback that was not registered fails.
        /// </summary>
        [Fact]
        public void AsyncExecutionEvent_CantUnregisterNotRegistered()
        {
            using (var executionEvent = new AsyncExecutionEvent<object, bool>())
            {
                AsyncExecutionEventCallback<object, bool> callbackAsync = (sender, arg) =>
                {
                    return Task.CompletedTask;
                };

                Assert.Throws<ArgumentException>(() => executionEvent.Unregister(callbackAsync));
            }
        }

        /// <summary>
        /// Checks that if multiple callbacks are registered, they are all called and they are called in correct order.
        /// </summary>
        [Fact]
        public async void AsyncExecutionEvent_MultipleCallbacksExecutedInCorrectOrder_Async()
        {
            using (var executionEvent = new AsyncExecutionEvent<object, object>())
            {
                int orderIndex = 0;
                var orderList = new List<int>();

                AsyncExecutionEventCallback<object, object> callback1Async = (sender, arg) =>
                {
                    orderIndex++;
                    orderList.Add(orderIndex);
                    return Task.CompletedTask;
                };

                AsyncExecutionEventCallback<object, object> callback2Async = (sender, arg) =>
                {
                    orderIndex *= 3;
                    orderList.Add(orderIndex);
                    return Task.CompletedTask;
                };

                AsyncExecutionEventCallback<object, object> callback3Async = (sender, arg) =>
                {
                    orderIndex = (orderIndex + 2) * 2;
                    orderList.Add(orderIndex);
                    return Task.CompletedTask;
                };

                // We register the callbacks so that they should execute in order 1, 2, 3.
                executionEvent.Register(callback2Async);
                executionEvent.Register(callback1Async, true);
                executionEvent.Register(callback3Async);

                // We execute the callbacks and remove some of them and execute again.
                await executionEvent.ExecuteCallbacksAsync(null, null);

                executionEvent.Unregister(callback1Async);
                executionEvent.Unregister(callback3Async);

                await executionEvent.ExecuteCallbacksAsync(null, null);

                // So the final execution sequence should have been 1, 2, 3, 2, we check that.
                Assert.Equal(1, orderList[0]);  // 0 + 1 = 1
                Assert.Equal(3, orderList[1]);  // 1 * 3 = 3
                Assert.Equal(10, orderList[2]); // (3 + 2) * 2 = 10
                Assert.Equal(30, orderList[3]);  // 10 * 3 = 30
                Assert.Equal(30, orderIndex);

                // After we unregister all callbacks, execution should not change the value of orderIndex.
                executionEvent.Unregister(callback2Async);
                await executionEvent.ExecuteCallbacksAsync(null, null);
                Assert.Equal(30, orderIndex);
            }
        }

        /// <summary>
        /// Checks that the callback method can unregister itself.
        /// </summary>
        [Fact]
        public async void AsyncExecutionEvent_CanUnregisterFromCallback_Async()
        {
            using (var executionEvent = new AsyncExecutionEvent<object, object>())
            {
                int value = 0;
                Task callback1Async(object sender, object arg)
                {
                    value++;

                    // Try to unregister itself.
                    executionEvent.Unregister(callback1Async);
                    return Task.CompletedTask;
                }

                async Task callback2Async(object sender, object arg)
                {
                    value++;

                    // Try to unregister itself, but first await a bit.
                    await Task.Delay(20);
                    executionEvent.Unregister(callback2Async);
                }

                executionEvent.Register(callback1Async);
                executionEvent.Register(callback2Async);

                // First execution should increment the value twice and unregister the callbacks.
                await executionEvent.ExecuteCallbacksAsync(null, null);

                Assert.Equal(2, value);

                // Second execution should do nothing as there should be no registered callbacks.
                await executionEvent.ExecuteCallbacksAsync(null, null);

                Assert.Equal(2, value);
            }
        }

        /// <summary>
        /// Checks that the callback method can unregister itself and dispose the execution event.
        /// </summary>
        [Fact]
        public async void AsyncExecutionEvent_CanDisposeFromCallback_Async()
        {
            var executionEvent = new AsyncExecutionEvent<object, object>();

            int value = 0;
            Task callbackAsync(object sender, object arg)
            {
                value++;

                // Try to unregister itself.
                executionEvent.Unregister(callbackAsync);
                executionEvent.Dispose();

                return Task.CompletedTask;
            }

            executionEvent.Register(callbackAsync);

            // Execution should increment the value and unregister the callbacks and dispose the execution event.
            await executionEvent.ExecuteCallbacksAsync(null, null);

            Assert.Equal(1, value);

            // After a while, no more executions are not possible because the event's lock is disposed.
            await Task.Delay(100);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await executionEvent.ExecuteCallbacksAsync(null, null));

            // Further attempts to dispose execution event won't do any harm.
            executionEvent.Dispose();
        }
    }
}
