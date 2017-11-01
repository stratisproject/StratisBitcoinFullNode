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
            var exception = Record.Exception(() => Guard.NotNull(obj, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void NullParameterNameThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, null));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void EmptyParameterNameThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, string.Empty));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void WhiteSpacesParameterNameThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, "   "));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void ValueDefinedObjectWithParameterNameDoesntThrow()
        {
            var exception = Record.Exception(() => Guard.NotNull(DateTime.UtcNow, "someDate"));
            Assert.Null(exception);
        }

        [Fact]
        public void EmptyStringThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotEmpty(string.Empty, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void NullStringThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotEmpty(null, "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact]
        public void WhiteSpacesStringThrowsArgumentNullException()
        {
            var exception = Record.Exception(() => Guard.NotEmpty("   ", "someObjectName"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void NotEmptyStringReturnsString()
        {
            var result = Guard.NotEmpty("value", "someObjectName");

            Assert.Equal("value", result);
        }

        [Fact]
        public void FalseAssertionThrowsException()
        {
            var exception = Record.Exception(() => Guard.Assert(false));
            Assert.NotNull(exception);
            Assert.IsType<Exception>(exception);
        }

        [Fact]
        public void TrueAssertionDoesntThrowException()
        {
            var exception = Record.Exception(() => Guard.Assert(true));
            Assert.Null(exception);
        }
    }
}
