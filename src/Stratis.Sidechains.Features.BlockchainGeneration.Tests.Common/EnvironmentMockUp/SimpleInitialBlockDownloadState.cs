using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp
{
    internal class SimpleInitialBlockDownloadState : IInitialBlockDownloadState
    {
        public bool IsInitialBlockDownload()
        {
            return false;
        }
    }
}
