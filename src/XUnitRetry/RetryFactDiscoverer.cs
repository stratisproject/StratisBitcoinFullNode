using System;
using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit
{
    public class RetryFactDiscoverer : IXunitTestCaseDiscoverer
    {
        readonly IMessageSink diagnosticMessageSink;

        public RetryFactDiscoverer(IMessageSink diagnosticMessageSink)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            var maxRetries = factAttribute.GetNamedArgument<int>("MaxRetries");
            if (maxRetries < 1)
                maxRetries = 3;

            var exponentialBackoffMs = Math.Max(0, factAttribute.GetNamedArgument<int>("ExponentialBackoffMs"));

            yield return new RetryTestCase(diagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod, maxRetries, exponentialBackoffMs);
        }
    }
}
