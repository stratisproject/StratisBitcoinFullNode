using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Stratis.FederatedPeg.Tests
{
    // Since this class uses the file system see the
    // IntegrationTests for detailed testing. 
    public class MemberFolderManager_Shall
    {
        [Fact]
        public void throw_when_no_folder()
        {
            Action act = () => new MemberFolderManager("random folder");
            act.Should().Throw<DirectoryNotFoundException>();
        }
    }
}
