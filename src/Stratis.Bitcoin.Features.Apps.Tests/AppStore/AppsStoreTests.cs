using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.Apps.Tests.AppStore
{
    public class AppsStoreTests
    {
        private readonly IAppsStore appStore;
        private readonly FakeAppsFileService fileService = new FakeAppsFileService();

        public AppsStoreTests()
        {
            this.appStore = new AppsStore(new Mock<ILoggerFactory>().Object, this.fileService);

        }
    }
}
