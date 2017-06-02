using Microsoft.AspNetCore.Mvc;

namespace Stratis.Bitcoin.Common.JsonErrors
{
    public class ErrorResult : ObjectResult
    {
	    public ErrorResult(int statusCode, ErrorResponse value) : base(value)
	    {
			StatusCode = statusCode;
		}
	}
}
