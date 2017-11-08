using System;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class ActionDisposableTest
    {
        [Fact]
        public void ConstructsWithAction()
        {
            bool onEnterCalled = false;
            Action onEnter = () => { onEnterCalled = true; };

            var disposable = new ActionDisposable(onEnter, () => { });

            Assert.True(onEnterCalled);
        }

        [Fact]
        public void DisposesWithAction()
        {
            bool onLeaveCalled = false;
            Action onLeave = () => { onLeaveCalled = true; };

            var disposable = new ActionDisposable(() => { }, onLeave);
            Assert.False(onLeaveCalled);

            disposable.Dispose();
            Assert.True(onLeaveCalled);
        }
    }
}
