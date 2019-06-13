using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// A generic result type.
    /// </summary>
    /// <typeparam name="T">The type of the value to return if the result was successful.</typeparam>
    public class ApiResult<T>
    {
        public readonly string ErrorMessage;

        public readonly bool Succeeded;

        public readonly T Value;

        [JsonConstructor]
        private ApiResult()
        {
        }

        internal ApiResult(bool succeeded, T value, string errorMessage)
        {
            this.Succeeded = succeeded;
            this.Value = value;
            this.ErrorMessage = errorMessage;
        }

        public static ApiResult<T> Ok(T value)
        {
            return new ApiResult<T>(true, value, null);
        }

        public static ApiResult<T> Fail(string errorMessage)
        {
            return new ApiResult<T>(false, default(T), errorMessage);
        }
    }
}
