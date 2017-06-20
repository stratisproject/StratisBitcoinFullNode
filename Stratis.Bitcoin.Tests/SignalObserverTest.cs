using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Moq;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Tests.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{  
    public class SignalObserverTest : LogsTestBase
    {
        SignalObserver<Block> observer;               

        protected override void Initialize()
        {
            this.observer = new TestBlockSignalObserver();
        }

        [TestMethod]
        public void SignalObserverLogsSignalOnError()
        {
            var exception = new InvalidOperationException("This should not have occurred!");

            this.observer.OnError(exception);

            AssertLog(this.FullNodeLogger, LogLevel.Error, exception.ToString());
        }            

        private class TestBlockSignalObserver : SignalObserver<Block>
        {
            public TestBlockSignalObserver()
            {
            }

            protected override void OnNextCore(Block value)
            {

            }
        }
    }
}
