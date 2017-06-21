using Microsoft.VisualStudio.TestTools.UnitTesting;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Tests.Utilities
{
    [TestClass]
    public class ActionDisposableTest
    {
        [TestMethod]
        public void ConstructsWithAction()
        {
            bool onEnterCalled = false;
            Action onEnter = () => { onEnterCalled = true; };

            var disposable = new ActionDisposable(onEnter, () => { });

            Assert.IsTrue(onEnterCalled);
        }

        [TestMethod]
        public void DisposesWithAction()
        {
            bool onLeaveCalled = false;
            Action onLeave = () => { onLeaveCalled = true; };

            var disposable = new ActionDisposable(() => { }, onLeave);
            Assert.IsFalse(onLeaveCalled);

            disposable.Dispose();
            Assert.IsTrue(onLeaveCalled);
        }
    }
}
