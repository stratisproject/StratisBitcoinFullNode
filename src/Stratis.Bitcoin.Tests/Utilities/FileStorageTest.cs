using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class FileStorageTest : LogsTestBase, IDisposable
    {
        public class TestObject
        {
            public string Property1 { get; set; }

            public string Property2 { get; set; }
        }

        private const string TestFolder = "TestData/FileStorageTest";

        [Fact]
        public void Given_ConstructorIsCalled_TheSpecifiedFolderShouldBeCreated()
        {
            // Arrange
            string dir = this.GetFolderPathForTestExecution();

            // Act
            var fileStorage = new FileStorage<TestObject>(dir);

            // Assert
            Assert.True(Directory.Exists(fileStorage.FolderPath));
            var directoryInfo = new DirectoryInfo(fileStorage.FolderPath);
            Assert.True(!directoryInfo.EnumerateFiles().Any());
            Assert.True(!directoryInfo.EnumerateDirectories().Any());
        }

        [Fact]
        public void Given_SaveToFileIsCalled_TheObjectIsSavedProperly()
        {
            // Arrange
            string dir = this.GetFolderPathForTestExecution();

            // Act
            var fileStorage = new FileStorage<TestObject>(dir);

            // Assert
            Assert.True(Directory.Exists(fileStorage.FolderPath));
            var directoryInfo = new DirectoryInfo(fileStorage.FolderPath);
            Assert.True(!directoryInfo.EnumerateFiles().Any());
            Assert.True(!directoryInfo.EnumerateDirectories().Any());
        }

        [Fact]
        public void GivenExistsIsCalled_WhenTheFileExists_ThenTrueIsreturned()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json");

            // Act
            bool result = fileStorage.Exists("savedTestObject.json");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void GivenExistsIsCalled_WhenTheFileDoesntExist_ThenFalseIsreturned()
        {
            // Arrange
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);

            // Act
            bool result = fileStorage.Exists("savedTestObject.json");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GivenGetFilesPathsIsCalled_WhenFilesWithTheRightExtensionExist_ThenTheFilesAreReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            IEnumerable<string> filesPaths = fileStorage.GetFilesPaths("json");

            // Assert
            Assert.Equal(2, filesPaths.Count());
            Assert.Contains(Path.Combine(dir, "savedTestObject1.json"), filesPaths);
            Assert.Contains(Path.Combine(dir, "savedTestObject2.json"), filesPaths);
        }

        [Fact]
        public void GivenGetFilesPathsIsCalled_WhenNoFilesWithTheRightExtensionExist_ThenNoFilesAreReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            IEnumerable<string> filesPaths = fileStorage.GetFilesPaths("txt");

            // Assert
            Assert.Empty(filesPaths);
            Assert.DoesNotContain(Path.Combine(dir, "savedTestObject1.json"), filesPaths);
            Assert.DoesNotContain(Path.Combine(dir, "savedTestObject2.json"), filesPaths);
        }

        [Fact]
        public void GivenGetFilesNamesIsCalled_WhenNoFilesWithTheRightExtensionExist_ThenNoFilesAreReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            IEnumerable<string> filesPaths = fileStorage.GetFilesNames("txt");

            // Assert
            Assert.Empty(filesPaths);
            Assert.DoesNotContain("savedTestObject1.json", filesPaths);
            Assert.DoesNotContain("savedTestObject2.json", filesPaths);
        }

        [Fact]
        public void GivenGetFilesNamesIsCalled_WhenFilesWithTheRightExtensionExist_ThenFilesAreReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            IEnumerable<string> filesPaths = fileStorage.GetFilesNames("json");

            // Assert
            Assert.Equal(2, filesPaths.Count());
            Assert.Contains("savedTestObject1.json", filesPaths);
            Assert.Contains("savedTestObject2.json", filesPaths);
        }

        [Fact]
        public void GivenLoadByFileNameIsCalled_WhenNoFileWithTheNameExist_ThenFileNotFoundExceptionIsThrown()
        {
            // Arrange
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);

            // Act
            Assert.Throws<FileNotFoundException>(() => fileStorage.LoadByFileName("myfile.txt"));
        }

        [Fact]
        public void GivenLoadByFileNameIsCalled_WhenAFileWithTheNameExist_ThenTheObjectIsReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");

            // Act
            TestObject loadedObject = fileStorage.LoadByFileName("savedTestObject1.json");

            // Assert
            Assert.Equal(testObject1.Property1, loadedObject.Property1);
            Assert.Equal(testObject1.Property2, loadedObject.Property2);
        }

        [Fact]
        public void GivenLoadByFileExtensionIsCalled_WhenFilesExist_ThenTheObjectsAreReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop3", Property2 = "prop4" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            IEnumerable<TestObject> loadedObjects = fileStorage.LoadByFileExtension("json");

            // Assert
            Assert.Equal(2, loadedObjects.Count());
            Assert.Contains(loadedObjects, o => o.Property1 == testObject1.Property1 && o.Property2 == testObject1.Property2);
            Assert.Contains(loadedObjects, o => o.Property1 == testObject2.Property1 && o.Property2 == testObject2.Property2);
        }

        [Fact]
        public void GivenLoadByFileExtensionIsCalled_WhenFilesDontExist_ThenNoObjectsIsReturned()
        {
            // Arrange
            var testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            var testObject2 = new TestObject { Property1 = "prop3", Property2 = "prop4" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.txt");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.txt");

            // Act
            IEnumerable<TestObject> loadedObjects = fileStorage.LoadByFileExtension("json");

            // Assert
            Assert.False(loadedObjects.Any());
        }

        [Fact]
        public void GivenSaveWithBackupIsCalled_WhenTheFileExistsandWasChanged_ThenABackupIsSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json", true);
            testObject.Property1 = testObject.Property1 + "-changed";
            fileStorage.SaveToFile(testObject, "savedTestObject.json", true);

            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");
            
            // Assert
            Assert.True(isFileExists);
            Assert.True(isBackupFileExists);

            TestObject loadedByFileName = fileStorage.LoadByFileName("savedTestObject.json");
            TestObject loadedBackupByFileName = fileStorage.LoadByFileName("savedTestObject.json.bak");

            Assert.Equal("prop1", loadedBackupByFileName.Property1);
            Assert.Equal("prop2", loadedBackupByFileName.Property2);
            Assert.Equal("prop1-changed", loadedByFileName.Property1);
            Assert.Equal("prop2", loadedByFileName.Property2);
        }

        [Fact]
        public void GivenSaveWithBackupIsCalled_WhenTheFileDidntExist_ThenTwoIdenticalFilesAreSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json", true);
            
            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");

            // Assert
            Assert.True(isFileExists);
            Assert.True(isBackupFileExists);

            TestObject loadedByFileName = fileStorage.LoadByFileName("savedTestObject.json");
            TestObject loadedBackupByFileName = fileStorage.LoadByFileName("savedTestObject.json.bak");

            Assert.Equal("prop1", loadedBackupByFileName.Property1);
            Assert.Equal("prop2", loadedBackupByFileName.Property2);
            Assert.Equal("prop1", loadedByFileName.Property1);
            Assert.Equal("prop2", loadedByFileName.Property2);
        }

        [Fact]
        public void GivenSaveFileWithNoBackupIsCalled_WhenTheFileExists_ThenNoBackupIsSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json", false);
            testObject.Property1 = testObject.Property1 + "-changed";
            fileStorage.SaveToFile(testObject, "savedTestObject.json");

            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");

            // Assert
            Assert.True(isFileExists);
            Assert.False(isBackupFileExists);
            TestObject loadedByFileName = fileStorage.LoadByFileName("savedTestObject.json");
            Assert.Equal("prop1-changed", loadedByFileName.Property1);
            Assert.Equal("prop2", loadedByFileName.Property2);
        }

        [Fact]
        public void GivenSaveFileWithNoBackupIsCalled_WhenTheFileDidntExist_ThenNoBackupIsSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json", false);

            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");

            // Assert
            Assert.True(isFileExists);
            Assert.False(isBackupFileExists);
        }

        [Fact]
        public void GivenSaveFileIsCalled_WhenTheFileDidntExist_ThenNoBackupIsSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json");

            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");

            // Assert
            Assert.True(isFileExists);
            Assert.False(isBackupFileExists);
        }

        [Fact]
        public void GivenSaveFileIsCalled_WhenTheFileExists_ThenNoBackupIsSaved()
        {
            // Arrange
            var testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = this.GetFolderPathForTestExecution();
            var fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject, "savedTestObject.json");
            testObject.Property1 = testObject.Property1 + "-changed";
            fileStorage.SaveToFile(testObject, "savedTestObject.json");

            // Act
            bool isFileExists = fileStorage.Exists("savedTestObject.json");
            bool isBackupFileExists = fileStorage.Exists("savedTestObject.json.bak");

            // Assert
            Assert.True(isFileExists);
            Assert.False(isBackupFileExists);
            TestObject loadedByFileName = fileStorage.LoadByFileName("savedTestObject.json");
            Assert.Equal("prop1-changed", loadedByFileName.Property1);
            Assert.Equal("prop2", loadedByFileName.Property2);
        }

        public void Dispose()
        {
            var directoryInfo = new DirectoryInfo(TestFolder);

            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        private string GetFolderPathForTestExecution([System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
        {
            return Path.Combine(TestFolder, methodName);
        }
    }
}
