using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Moq;
using Breeze.Wallet.Wrappers;
using Breeze.Wallet;
using Breeze.Wallet.Controllers;
using Breeze.Wallet.Models;

namespace Breeze.Api.Tests
{
    public class ControllersTests
    {
        [Fact]
        public void CreateSafeSuccessfullyReturnsMnemonic()
        {
            var mockSafeCreate = new Mock<ISafeWrapper>();
            mockSafeCreate.Setup(safe => safe.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("mnemonic");

            var controller = new SafeController(mockSafeCreate.Object);

            // Act
            var result = controller.Create(new SafeCreationModel
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = ""
            });

            // Assert
            mockSafeCreate.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.Equal("mnemonic", viewResult.Value);
            Assert.NotNull(result);
        }

        [Fact]
        public void LoadSafeSuccessfullyReturnsSafeModel()
        {
            SafeModel safeModel = new SafeModel
            {
                FileName = "myWallet",
                Network = "MainNet",
                Addresses = new List<string> { "address1", "address2", "address3", "address4", "address5" }

            };
            var mockSafeWrapper = new Mock<ISafeWrapper>();
            mockSafeWrapper.Setup(safe => safe.Recover(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(safeModel);

            var controller = new SafeController(mockSafeWrapper.Object);

            // Act
            var result = controller.Recover(new SafeRecoveryModel
            {
                Name = "myName",
                FolderPath = "",
                Password = "",
                Network = "",
                Mnemonic = "mnemonic"
            });

            // Assert
            mockSafeWrapper.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(viewResult.Value);
            Assert.IsType<SafeModel>(viewResult.Value);

            var model = viewResult.Value as SafeModel;
            Assert.Equal("myWallet", model.FileName);
        }

        [Fact]
        public void RecoverSafeSuccessfullyReturnsSafeModel()
        {
            SafeModel safeModel = new SafeModel
            {
                FileName = "myWallet",
                Network = "MainNet",
                Addresses = new List<string> { "address1", "address2", "address3", "address4", "address5" }

            };
            var mockSafeWrapper = new Mock<ISafeWrapper>();
            mockSafeWrapper.Setup(safe => safe.Load(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(safeModel);

            var controller = new SafeController(mockSafeWrapper.Object);

            // Act
            var result = controller.Load(new SafeLoadModel
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockSafeWrapper.VerifyAll();
            var viewResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(viewResult.Value);
            Assert.IsType<SafeModel>(viewResult.Value);

            var model = viewResult.Value as SafeModel;
            Assert.Equal("myWallet", model.FileName);
        }

        [Fact]
        public void FileNotFoundExceptionandReturns404()
        {
            var mockSafeWrapper = new Mock<ISafeWrapper>();
            mockSafeWrapper.Setup(safe => safe.Load(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Throws<FileNotFoundException>();
            
            var controller = new SafeController(mockSafeWrapper.Object);

            // Act
            var result = controller.Load(new SafeLoadModel
            {
                Name = "myName",
                FolderPath = "",
                Password = ""
            });

            // Assert
            mockSafeWrapper.VerifyAll();
            var viewResult = Assert.IsType<ObjectResult>(result);
            Assert.NotNull(viewResult);		
            Assert.Equal(404, viewResult.StatusCode);
        }

    }
}
