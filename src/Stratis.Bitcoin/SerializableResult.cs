using Newtonsoft.Json;

namespace Stratis.Bitcoin
{
    /// <summary>
    /// A generic result type that can be serialized.
    /// </summary>
    /// <typeparam name="T">The type of the value to return if the result was successful.</typeparam>
    public sealed class SerializableResult<T>
    {
        [JsonProperty("isFailure")]
        public bool IsSuccess { get; private set; }

        [JsonProperty("message")]
        public string Message { get; private set; }

        [JsonProperty("value")]
        public T Value { get; private set; }

        [JsonConstructor]
        private SerializableResult()
        {
        }

        private SerializableResult(bool isFailure, T value, string error)
        {
            this.IsSuccess = !isFailure;
            this.Value = value;
            this.Message = error;
        }

        public static SerializableResult<T> Ok(T value)
        {
            return new SerializableResult<T>(true, value, null);
        }

        public static SerializableResult<T> Fail(string error)
        {
            return new SerializableResult<T>(false, default(T), error);
        }
    }
}