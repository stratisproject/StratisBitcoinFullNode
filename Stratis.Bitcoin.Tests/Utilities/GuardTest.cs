using System;
using Stratis.Bitcoin.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{
    [TestClass]
    public class GuardTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullValueThrowsArgumentNullException()
        {
            object obj = null;
            Guard.NotNull(obj, "someObjectName");           
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullParameterNameThrowsArgumentNullException()
        {
            Guard.NotNull(DateTime.Now, null);            
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void EmptyParameterNameThrowsArgumentNullException()
        {
            Guard.NotNull(DateTime.Now, string.Empty);            
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WhiteSpacesParameterNameThrowsArgumentNullException()
        {
            Guard.NotNull(DateTime.Now, "   ");            
        }

        [TestMethod]
        public void ValueDefinedObjectWithParameterNameDoesntThrow()
        {
            Guard.NotNull(DateTime.Now, "someDate");            
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EmptyStringThrowsArgumentNullException()
        {
            Guard.NotEmpty(string.Empty, "someObjectName");            
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullStringThrowsArgumentNullException()
        {
            Guard.NotEmpty(null, "someObjectName");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WhiteSpacesStringThrowsArgumentNullException()
        {
            Guard.NotEmpty("   ", "someObjectName");            
        }

        [TestMethod]
        public void NotEmptyStringReturnsString()
        {
            var result = Guard.NotEmpty("value", "someObjectName");

            Assert.AreEqual(result, "value");
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void FalseAssertionThrowsException()
        {
            Guard.Assert(false);            
        }

        [TestMethod]
        public void TrueAssertionDoesntThrowException()
        {
            Guard.Assert(true);            
        }
    }
}
