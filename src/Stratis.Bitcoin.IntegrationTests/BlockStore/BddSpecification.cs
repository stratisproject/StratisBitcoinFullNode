using System;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public abstract class BddSpecification
    {
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