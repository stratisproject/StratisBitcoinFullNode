using System;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.TestFramework
{
    public abstract class BddSpecification : IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly DateTime startOfTestTime;

        protected BddSpecification(ITestOutputHelper output)
        {
            this.output = output;
            this.startOfTestTime = DateTime.UtcNow;
            this.BeforeTest();
        }

        public void Dispose()
        {
            this.AfterTest();
            var endOfTestTime = DateTime.UtcNow;
            this.output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) [End of test - {(endOfTestTime - this.startOfTestTime).TotalSeconds} seconds.]");
        }

        protected abstract void BeforeTest();
        protected abstract void AfterTest();

        public void Given(Action action)
        {
            this.RunStep(action, "Given");
        }

        public void When(Action action)
        {
            this.RunStep(action, "When");
        }

        public void Then(Action action)
        {
            this.RunStep(action, "Then");
        }

        public void And(Action action)
        {
            this.RunStep(action, "And");
        }

        private void RunStep(Action action, string stepType)
        {
            this.output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) {stepType} {action.Method.Name.Replace("_", " ")}");
            action.Invoke();
        }
    }
}