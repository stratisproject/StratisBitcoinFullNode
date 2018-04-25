using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.TestFramework
{
    [DebuggerStepThrough()]
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

        public void Given(Action step)
        {
            this.RunStep(step);
        }
        public void Given(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, ct);
        }

        public void When(Action step)
        {
            this.RunStep(step);
        }

        public void When(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, ct);
        }

        public void Then(Action step)
        {
            this.RunStep(step);
        }

        public void Then(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, ct);
        }

        public void And(Action step)
        {
            this.RunStep(step);
        }

        public void And(Func<Task> step, CancellationToken ct = default(CancellationToken))
        {
            this.RunStep(step, ct);
        }

        private void RunStep(Action step, [CallerMemberName] string stepType = null)
        {
            OuputStepDetails(step.Method.Name, stepType);
            step.Invoke();
        }

        private void RunStep(Func<Task> step, CancellationToken ct = default(CancellationToken), [CallerMemberName] string stepType = null)
        {
            OuputStepDetails(step.Method.Name, stepType);

            try
            {
                Task.Run(step, ct).Wait(ct);
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions) { throw innerException; }
            };
        }

        private void OuputStepDetails(string stepRawName, string stepType)
        {
            this.output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) {stepType} {stepRawName.Replace("_", " ")}");
        }
    }
}