using System;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class GuardTest
    {
        [Fact]
        public void NullValueThrowsArgumentNullException()
        {
            object obj = null;
            Exception exception = Record.Exception(() => Guard.NotNull(obj, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void NullParameterNameThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void EmptyParameterNameThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, string.Empty));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void WhiteSpacesParameterNameThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, "   "));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void ValueDefinedObjectWithParameterNameDoesntThrow()
        {
            Exception exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, "someDate"));
            Assert.Null(exception);
        }

        [Fact]
        public void EmptyStringThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotEmpty(string.Empty, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void NullStringThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotEmpty(null, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void WhiteSpacesStringThrowsArgumentNullException()
        {
            Exception exception = Record.Exception(() => Guard.NotEmpty("   ", "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void NotEmptyStringReturnsString()
        {
            string result = Guard.NotEmpty("value", "someObjectName");

            Assert.Equal("value", result);
        }

        [Fact]
        public void FalseAssertionThrowsException()
        {
            Exception exception = Record.Exception(() => Guard.Assert(false));
            Assert.NotNull(exception);
            Assert.IsType<Exception>(exception);
        }

        [Fact]
        public void TrueAssertionDoesntThrowException()
        {
            Exception exception = Record.Exception(() => Guard.Assert(true));
            Assert.Null(exception);
        }
    }
}
