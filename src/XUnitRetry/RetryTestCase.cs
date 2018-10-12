using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit
{
    [Serializable]
    public class RetryTestCase : XunitTestCase
    {
        private int maxRetries;
        private int exponentialBackoffMs;

        private static readonly int MaxDelayInMs = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer", true)]
        public RetryTestCase() { }

        public RetryTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay testMethodDisplay, ITestMethod testMethod, int maxRetries, int exponentialBackoffMs)
            : base(diagnosticMessageSink, testMethodDisplay, testMethod, testMethodArguments: null)
        {
            this.maxRetries = maxRetries;
            this.exponentialBackoffMs = exponentialBackoffMs;
        }

        // This method is called by the xUnit test framework classes to run the test case. We will do the
        // loop here, forwarding on to the implementation in XunitTestCase to do the heavy lifting. We will
        // continue to re-run the test until the aggregator has an error (meaning that some internal error
        // condition happened), or the test runs without failure, or we've hit the maximum number of tries.
        public override async Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink,
                                                        IMessageBus messageBus,
                                                        object[] constructorArguments,
                                                        ExceptionAggregator aggregator,
                                                        CancellationTokenSource cancellationTokenSource)
        {
            var runCount = 0;

            while (true)
            {
                // This is really the only tricky bit: we need to capture and delay messages (since those will
                // contain run status) until we know we've decided to accept the final result;
                var delayedMessageBus = new DelayedMessageBus(messageBus);
                
                var summary = await base.RunAsync(diagnosticMessageSink, delayedMessageBus, constructorArguments, aggregator, cancellationTokenSource);
                if (aggregator.HasExceptions || summary.Failed == 0 || ++runCount >= maxRetries)
                {
                    delayedMessageBus.Dispose();  // Sends all the delayed messages
                    return summary;
                }

                diagnosticMessageSink.OnMessage(new DiagnosticMessage("Execution of '{0}' failed (attempt #{1}), retrying...", DisplayName, runCount));

                var delayInMs = Math.Min((int)Math.Pow(2, runCount - 1) * this.exponentialBackoffMs, MaxDelayInMs);
                await Task.Delay(delayInMs);
            }
        }

        public override void Serialize(IXunitSerializationInfo data)
        {
            base.Serialize(data);

            data.AddValue("MaxRetries", maxRetries);
            data.AddValue("ExponentialBackoffMs", exponentialBackoffMs);
        }

        public override void Deserialize(IXunitSerializationInfo data)
        {
            base.Deserialize(data);

            maxRetries = data.GetValue<int>("MaxRetries");
            exponentialBackoffMs = data.GetValue<int>("ExponentialBackoffMs");
        }
    }
}
