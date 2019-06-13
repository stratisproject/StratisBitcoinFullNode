using Newtonsoft.Json;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// A generic result type.
    /// </summary>
    /// <typeparam name="T">The type of the value to return if the result was successful.</typeparam>
    public class ApiResult<T>
    {
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; private set; }

        [JsonProperty("succeeded")]
        public bool Succeeded { get; private set; }

        [JsonProperty("value")]
        public T Value { get; private set; }

        [JsonConstructor]
        private ApiResult()
        {
        }

        public ApiResult(bool succeeded, T value, string errorMessage)
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
