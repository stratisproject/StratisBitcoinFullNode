using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.Tests.Common.TestFramework
{
    [DebuggerStepThrough]
    public abstract class BddSpecification : IDisposable
    {
        protected readonly ITestOutputHelper Output;
        private readonly DateTime startOfTestTime;
        
        private ITest currentTest;

        /// <summary>
        /// This can for instance be used to find the name of the test currently using the
        /// class by calling this.CurrentTest.DisplayName
        /// </summary>
        protected ITest CurrentTest => this.currentTest ?? (this.currentTest = this.Output?.GetType()
                                           .GetField("test", BindingFlags.Instance | BindingFlags.NonPublic)
                                           .GetValue(this.Output) as ITest);

        protected BddSpecification(ITestOutputHelper output)
        {
            this.Output = output;
            this.startOfTestTime = DateTime.UtcNow;

            this.BeforeTest();
        }

        public void Dispose()
        {
            this.AfterTest();

            DateTime endOfTestTime = DateTime.UtcNow;
            this.Output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) [End of test - {(endOfTestTime - this.startOfTestTime).TotalSeconds} seconds.]");
        }

        protected abstract void BeforeTest();
        protected abstract void AfterTest();

        public void Given(Action step)
        {
            this.RunStep(step);
        }

        public void Given(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.RunStep(step, cancellationToken);
        }

        public void When(Action step)
        {
            this.RunStep(step);
        }

        public void When(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.RunStep(step, cancellationToken);
        }

        public void Then(Action step)
        {
            this.RunStep(step);
        }

        public void Then(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.RunStep(step, cancellationToken);
        }

        public void And(Action step)
        {
            this.RunStep(step);
        }

        public void And(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.RunStep(step, cancellationToken);
        }

        public void But(Action step)
        {
            this.RunStep(step);
        }

        public void But(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.RunStep(step, cancellationToken);
        }

        private void RunStep(Action step, [CallerMemberName] string stepType = null)
        {
            OuputStepDetails(step.Method.Name, stepType);
            step.Invoke();
        }

        private void RunStep(Func<Task> step, CancellationToken cancellationToken = default(CancellationToken), [CallerMemberName] string stepType = null)
        {
            OuputStepDetails(step.Method.Name, stepType);

            try
            {
                Task.Run(step, cancellationToken).Wait(cancellationToken);
            }
            catch (AggregateException ex)
            {
                foreach (Exception innerException in ex.InnerExceptions) { throw innerException; }
            };
        }

        private void OuputStepDetails(string stepRawName, string stepType)
        {
            this.Output?.WriteLine($"({DateTime.UtcNow.ToLongTimeString()}) {stepType} {stepRawName.Replace("_", " ")}");
        }
    }
}