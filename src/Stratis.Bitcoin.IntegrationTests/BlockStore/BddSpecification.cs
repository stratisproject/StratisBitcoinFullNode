using System;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public abstract class BddSpecification : IDisposable
    {
        protected BddSpecification()
        {
            this.BeforeTest();
        }

        protected virtual void BeforeTest() {}

        protected virtual void AfterTest() {}

        public void Dispose()
        {
            this.AfterTest();
        }

        public void Given(Action action)
        {
            action.Invoke();
        }

        public void When(Action action)
        {
            action.Invoke();
        }

        public void Then(Action action)
        {
            action.Invoke();
        }

        public void And(Action action)
        {
            action.Invoke();
        }
    }
}