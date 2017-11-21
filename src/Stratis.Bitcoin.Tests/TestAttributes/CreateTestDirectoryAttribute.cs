using System.IO;
using System.Reflection;
using Xunit.Sdk;

namespace Stratis.Bitcoin.Tests.TestAttributes
{
    public class CreateTestDirectoryAttribute : BeforeAfterTestAttribute
    {
        private const string CustomDirectoryTemplate = "TestData/{0}/{1}";

        private readonly string customFolderPath;

        public CreateTestDirectoryAttribute(string customFolderPath = null)
        {
            this.customFolderPath = customFolderPath;
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            if (string.IsNullOrEmpty(this.customFolderPath))
            {
                TestBase.AssureEmptyDir(string.Format(CustomDirectoryTemplate, methodUnderTest.DeclaringType.Name, methodUnderTest.Name));
                return;
            }

            TestBase.AssureEmptyDir(this.customFolderPath);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            string directory = string.Format(CustomDirectoryTemplate, methodUnderTest.DeclaringType.Name, methodUnderTest.Name);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
