using Newtonsoft.Json;

namespace Stratis.Features.FederatedPeg.SourceChain
{
    /// <summary>
    /// A generic result type.
    /// </summary>
    /// <typeparam name="T">The type of the value to return if the result was successful.</typeparam>
    public sealed class Result<T>
    {
        [JsonProperty("isFailure")]
        public bool IsSuccess { get; private set; }

        [JsonProperty("message")]
        public string Message { get; private set; }

        [JsonProperty("value")]
        public T Value { get; private set; }

        [JsonConstructor]
        private Result()
        {

        }

        private Result(bool isFailure, T value, string error)
        {
            this.IsSuccess = !isFailure;
            this.Value = value;
            this.Message = error;
        }

        public static Result<T> Ok(T value)
        {
            return new Result<T>(true, value, null);
        }

        public static Result<T> Fail(string error)
        {
            return new Result<T>(false, default(T), error);
        }
    }
}