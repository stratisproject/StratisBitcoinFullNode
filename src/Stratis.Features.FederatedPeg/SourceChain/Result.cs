namespace Stratis.Features.FederatedPeg.SourceChain
{
    /// <summary>
    /// A generic result type.
    /// </summary>
    /// <typeparam name="T">The type of the value to return if the result was successful.</typeparam>
    public struct Result<T>
    {
        public bool IsFailure { get; }
        public bool IsSuccess { get; }
        public string Error { get; }
        public T Value { get; }

        internal Result(bool isFailure, T value, string error)
        {
            this.IsFailure = isFailure;
            this.IsSuccess = !isFailure;
            this.Value = value;
            this.Error = error;
        }

        public static Result<T> Ok(T value)
        {
            return new Result<T>(false, value, null);
        }

        public static Result<T> Fail(string error)
        {
            return new Result<T>(true, default(T), error);
        }
    }
}