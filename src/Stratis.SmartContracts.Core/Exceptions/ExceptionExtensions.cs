using System;

namespace Stratis.SmartContracts.Core.Exceptions
{
    public static class ExceptionExtensions
    {
        public static TException WithReasons<TException>(this TException e, params string[] reasons) where TException : Exception
        {
            return e.WithData("Reasons", reasons);
        }

        public static TException WithData<TException>(this TException e, string key, object value) where TException : Exception
        {
            e.Data[key] = value;
            return e;
        }

        public static string GetFullMessage<TException>(this TException e) where TException : Exception
        {
            var message = (e.Message.EndsWith(".") ? e.Message : e.Message + ".") +
                          (e.Data.Contains("Reasons") && e.Data["Reasons"] is string[]
                              ? " Reasons: " + string.Join("; ", (string[])e.Data["Reasons"])
                              : string.Empty);

            return message;
        }
    }
}
