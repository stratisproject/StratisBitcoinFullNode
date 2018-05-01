using System.Collections.Generic;
using System.Net;

namespace Stratis.SmartContracts.Standards
{
    public enum ResultStatus
    {
        Success,
        Error
    }

    public class Result
    {
        public ResultStatus Status { get; }
        public bool Success => this.Status == ResultStatus.Success;

        public string Message { get; }

        public List<string> Messages { get; }

        public bool Failure => !this.Success;

        protected Result(ResultStatus status, string message = null)
        {
            this.Status = status;
            this.Message = message;
        }

        protected Result(ResultStatus status, List<string> messages)
        {
            this.Status = status;
            this.Messages = messages;
        }

        public static Result Fail(string message)
        {
            return new Result(ResultStatus.Error, message);
        }

        public static Result<T> Fail<T>(string message)
        {
            return new Result<T>(default(T), ResultStatus.Error, message);
        }

        public static Result<T> Fail<T>(List<string> messages)
        {
            return new Result<T>(default(T), ResultStatus.Error, messages);
        }

        public static Result Ok()
        {
            return new Result(ResultStatus.Success, string.Empty);
        }

        public static Result<T> Ok<T>(T value)
        {
            return new Result<T>(value, ResultStatus.Success, string.Empty);
        }

        public static Result Combine(params Result[] results)
        {
            foreach (Result result in results)
            {
                if (result.Failure)
                    return result;
            }

            return Ok();
        }
    }


    public class Result<T> : Result
    {
        public T Value { get; }

        protected internal Result(T value, ResultStatus status, string message = null)
            : base(status, message)
        {
            this.Value = value;
        }

        protected internal Result(T value, ResultStatus status, List<string> messages)
            : base(status, messages)
        {
            this.Value = value;
        }
    }
}
