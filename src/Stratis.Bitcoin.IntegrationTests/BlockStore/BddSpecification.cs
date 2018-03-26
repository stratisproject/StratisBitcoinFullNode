using System;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public abstract class BddSpecification : IDisposable
    {
        protected readonly ITestOutputHelper output;

        protected BddSpecification()
        {
            this.BeforeTest();
        }

        protected BddSpecification(ITestOutputHelper output) : this()
        {
            this.output = output;
        }

        public void Dispose()
        {
            this.AfterTest();
        }

        protected abstract void BeforeTest();
        protected abstract void AfterTest();

        public void Given(Action action)
        {
            RunStep(action, "Given");
        }

        public void When(Action action)
        {
            RunStep(action, "When");
        }

        public void Then(Action action)
        {
            RunStep(action, "Then");
        }

        public void And(Action action)
        {
            RunStep(action, "And");
        }

        private void RunStep(Action action, string stepType)
        {
            this.output?.WriteLine(stepType + " " + action.Method.Name.Replace("_", " "));
            action.Invoke();
        }
    }
}