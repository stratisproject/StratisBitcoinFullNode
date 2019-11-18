using System.Collections.Generic;
using System.Net;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Utilities.JsonErrors
{
    public static class ErrorHelpers
    {
        public static ErrorResult BuildErrorResponse(HttpStatusCode statusCode, string message, string description)
        {
            var errorResponse = new ErrorResponse
            {
                Errors = new List<ErrorModel>
                {
                    new ErrorModel
                    {
                        Status = (int) statusCode,
                        Message = message,
                        Description = description
                    }
                }
            };

            return new ErrorResult((int) statusCode, errorResponse);
        }

        public static ErrorResult MapToErrorResponse(this FeatureException featureException )
        {
            var errorResponse = new ErrorResponse
            {
                Errors = new List<ErrorModel>
                {
                    new ErrorModel
                    {
                        Status = (int) featureException.HttpStatusCode,
                        Message = featureException.Message,
                        Description = featureException.Description
                    }
                }
            };

            return new ErrorResult((int) featureException.HttpStatusCode, errorResponse);
        }
    }
}