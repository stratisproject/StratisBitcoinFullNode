using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Stratis.SmartContracts.Core.Exceptions;

namespace Stratis.SmartContracts.Standards
{
    public class Guard
    {
        private const string ValueCannotBeNull = "Value cannot be null";
        private const string NotAValidEnumValue = "Not a valid enum value";
        private const string ValueCannotBeNullOrEmpty = "Value cannot be null or empty";

        private static Exception DefaultExceptionFactory(string message, string paramName) => new ArgumentException(message, paramName);
        private static Exception RangeExceptionFactory(string message, string paramName) => new ArgumentOutOfRangeException(paramName, message);
        private static Exception NullExceptionFactory(string message, string paramName) => new ArgumentNullException(paramName, message);

        private static Func<T, bool> Null<T>() => value => value == null;
        private static Func<T, bool> InvalidEnum<T>() => value => !typeof(T).IsEnum || !Enum.IsDefined(typeof(T), value);
        private static readonly Func<string, bool> NullOrEmptyString = string.IsNullOrEmpty;
        private static readonly Func<Guid?, bool> NullOrEmptyGuid = value => value == null || value == Guid.Empty;

        public static void AgainstNull<T>(T value, string paramName = null, string message = null) where T : class
        {
            Against(Null<T>(), value, ValueCannotBeNull, paramName, message, NullExceptionFactory);
        }

        public static void AgainstNull<T>(T value, Func<Exception> exception) where T : class
        {
            Against(Null<T>(), value, ValueCannotBeNull, defaultExceptionFactory: NullExceptionFactory, customExceptionFactory: exception);
        }

        public static void AgainstInvalidEnum<T>(T value, string paramName = null, string message = null) where T : struct
        {
            Against(InvalidEnum<T>(), value, NotAValidEnumValue, paramName, message, RangeExceptionFactory);
        }

        public static void AgainstInvalidEnum<T>(T value, Func<Exception> exception) where T : struct
        {
            Against(InvalidEnum<T>(), value, NotAValidEnumValue, defaultExceptionFactory: RangeExceptionFactory, customExceptionFactory: exception);
        }

        public static void AgainstNullOrEmptyString(string value, string message = null, string paramName = null)
        {
            Against(NullOrEmptyString, value, ValueCannotBeNullOrEmpty, paramName, message);
        }

        public static void AgainstNullOrEmptyString(string value, Func<Exception> exception)
        {
            Against(NullOrEmptyString, value, ValueCannotBeNullOrEmpty, customExceptionFactory: exception);
        }

        public static void AgainstNullOrEmptyGuid(Guid? value, string message = null, string paramName = null)
        {
            Against(NullOrEmptyGuid, value, ValueCannotBeNullOrEmpty, paramName, message);
        }

        public static void AgainstNullOrEmptyGuid(Guid? value, Func<Exception> exception)
        {
            Against(NullOrEmptyGuid, value, ValueCannotBeNullOrEmpty, customExceptionFactory: exception);
        }

        public static void AgainstInvalidAddress(Address address, string message = null, string paramName = null)
        {
            Against(NullOrEmptyString, address.Value, ValueCannotBeNullOrEmpty, paramName, message);
        }

        public static void AgainstInvalidAddress(Address address, Func<Exception> exception)
        {
            Against(NullOrEmptyString, address.Value, ValueCannotBeNullOrEmpty, customExceptionFactory: exception);
        }

        public static void Against(Func<IEnumerable<string>> failureReasonsFunc, string message)
        {
            var reasons = failureReasonsFunc().ToArray();
            if (!reasons.Any()) { return; }

            throw new StandardTokenValidationException(message).WithReasons(reasons);
        }

        private static void Against<T>(Func<T, bool> guardPredicate,
            T value,
            string fallback = "Value was invalid",
            string paramName = null,
            string message = null,
            Func<string, string, Exception> defaultExceptionFactory = null,
            Func<Exception> customExceptionFactory = null)
        {
            if (!guardPredicate(value)) return;

            if (customExceptionFactory == null && !string.IsNullOrEmpty(message)) throw new StandardTokenValidationException(message);

            message = string.IsNullOrEmpty(message) ? fallback : message;

            Exception exception = null;

            if (customExceptionFactory != null)
                exception = customExceptionFactory();

            if (exception == null)
                exception = (defaultExceptionFactory ?? DefaultExceptionFactory)(message, paramName);

            throw exception;
        }
    }
}