using System;
using System.Net;

namespace Stratis.Bitcoin.Builder.Feature
{
    public class FeatureException : Exception
    {
        public FeatureException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public FeatureException(HttpStatusCode httpStatusCode, string message, string description,
            Exception innerException = null)
            : this(message, innerException)
        {
            this.HttpStatusCode = httpStatusCode;
            this.Description = description;
        }

        public HttpStatusCode HttpStatusCode { get; set; }

        public string Description { get; set; }
    }
}