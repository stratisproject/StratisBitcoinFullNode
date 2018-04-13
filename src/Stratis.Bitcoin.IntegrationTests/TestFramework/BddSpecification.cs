using System;
using System.Threading;
using System.Threading.Tasks;
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
        public void Given(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, "Given", ct);
        }

        public void When(Action action)
        {
            this.RunStep(action, "When");
        }

        public void When(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, "When", ct);
        }

        public void Then(Action action)
        {
            this.RunStep(action, "Then");
        }

        public void Then(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, "Then", ct);
        }

        public void And(Action action)
        {
            this.RunStep(action, "And");
        }

        public void And(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, "And", ct);
        }

        private void RunStep(Action action, string stepType)
        {
            this.output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) {stepType} {action.Method.Name.Replace("_", " ")}");
            action.Invoke();
        }

        private void RunStep(Func<Task> step, string stepType, CancellationToken ct = default(CancellationToken))
        {
            this.output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) {stepType} {step.Method.Name.Replace("_", " ")}");

            try
            {
                var task = step.Invoke();
                Task.Run(() => task).Wait();
            }
            catch (AggregateException ex)
            {
                foreach(var innerException in ex.InnerExceptions) { throw innerException; }
            };
        }
    }
}