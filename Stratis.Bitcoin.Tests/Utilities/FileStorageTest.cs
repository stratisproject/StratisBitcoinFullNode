using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities.FileStorage;
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
            string dir = GetFolderPathForTestExecution();

            // Act
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);

            // Assert
            Assert.True(Directory.Exists(fileStorage.FolderPath));
            DirectoryInfo directoryInfo = new DirectoryInfo(fileStorage.FolderPath);
            Assert.True(!directoryInfo.EnumerateFiles().Any());
            Assert.True(!directoryInfo.EnumerateDirectories().Any());
        }

        [Fact]
        public void Given_SaveToFileIsCalled_TheObjectIsSavedProperly()
        {
            // Arrange
            string dir = GetFolderPathForTestExecution();

            // Act
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);

            // Assert
            Assert.True(Directory.Exists(fileStorage.FolderPath));
            DirectoryInfo directoryInfo = new DirectoryInfo(fileStorage.FolderPath);
            Assert.True(!directoryInfo.EnumerateFiles().Any());
            Assert.True(!directoryInfo.EnumerateDirectories().Any());
        }

        [Fact]
        public void GivenExistsIsCalled_WhenTheFileExists_ThenTrueIsreturned()
        {
            // Arrange
            TestObject testObject = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
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
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            
            // Act
            bool result = fileStorage.Exists("savedTestObject.json");

            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void GivenGetFilesPathsIsCalled_WhenFilesWithTheRightExtensionExist_ThenTheFilesAreReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            var filesPaths = fileStorage.GetFilesPaths("json");

            // Assert
            Assert.Equal(2, filesPaths.Count());
            Assert.Contains(Path.Combine(dir, "savedTestObject1.json"), filesPaths);
            Assert.Contains(Path.Combine(dir, "savedTestObject2.json"), filesPaths);
        }

        [Fact]
        public void GivenGetFilesPathsIsCalled_WhenNoFilesWithTheRightExtensionExist_ThenNoFilesAreReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            var filesPaths = fileStorage.GetFilesPaths("txt");

            // Assert
            Assert.Empty(filesPaths);
            Assert.DoesNotContain(Path.Combine(dir, "savedTestObject1.json"), filesPaths);
            Assert.DoesNotContain(Path.Combine(dir, "savedTestObject2.json"), filesPaths);
        }
        
        [Fact]
        public void GivenGetFilesNamesIsCalled_WhenNoFilesWithTheRightExtensionExist_ThenNoFilesAreReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            var filesPaths = fileStorage.GetFilesNames("txt");

            // Assert
            Assert.Empty(filesPaths);
            Assert.DoesNotContain("savedTestObject1.json", filesPaths);
            Assert.DoesNotContain("savedTestObject2.json", filesPaths);
        }
        
        [Fact]
        public void GivenGetFilesNamesIsCalled_WhenFilesWithTheRightExtensionExist_ThenFilesAreReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.json");

            // Act
            var filesPaths = fileStorage.GetFilesNames("json");

            // Assert
            Assert.Equal(2, filesPaths.Count());
            Assert.Contains("savedTestObject1.json", filesPaths);
            Assert.Contains("savedTestObject2.json", filesPaths);
        }


        [Fact]
        public void GivenLoadByFileNameIsCalled_WhenNoFileWithTheNameExist_ThenFileNotFoundExceptionIsThrown()
        {
            // Arrange
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            
            // Act
            Assert.Throws<FileNotFoundException>(() => fileStorage.LoadByFileName("myfile.txt"));
        }
        
        [Fact]
        public void GivenLoadByFileNameIsCalled_WhenAFileWithTheNameExist_ThenTheObjectIsReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.json");

            // Act
            var loadedObject = fileStorage.LoadByFileName("savedTestObject1.json");

            // Assert
            Assert.Equal(testObject1.Property1, loadedObject.Property1);
            Assert.Equal(testObject1.Property2, loadedObject.Property2);
        }
        
        [Fact]
        public void GivenLoadByFileExtensionIsCalled_WhenFilesExist_ThenTheObjectsAreReturned()
        {
            // Arrange
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop3", Property2 = "prop4" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
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
            TestObject testObject1 = new TestObject { Property1 = "prop1", Property2 = "prop2" };
            TestObject testObject2 = new TestObject { Property1 = "prop3", Property2 = "prop4" };
            string dir = GetFolderPathForTestExecution();
            FileStorage<TestObject> fileStorage = new FileStorage<TestObject>(dir);
            fileStorage.SaveToFile(testObject1, "savedTestObject1.txt");
            fileStorage.SaveToFile(testObject2, "savedTestObject2.txt");

            // Act
            IEnumerable<TestObject> loadedObjects = fileStorage.LoadByFileExtension("json");

            // Assert
            Assert.False(loadedObjects.Any());
        }

        public void Dispose()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(TestFolder);

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
