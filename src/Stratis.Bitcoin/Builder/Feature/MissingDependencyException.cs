using System;

namespace Stratis.Bitcoin.Builder.Feature
{
    /// <summary>
    /// Exception thrown when feature dependencies are missing.
    /// </summary>
    public class MissingDependencyException : Exception
    {
        /// <inheritdoc />
        public MissingDependencyException() 
            : base()
        {
        }

        /// <inheritdoc />
        public MissingDependencyException(string message) 
            : base(message)
        {
        }

        /// <inheritdoc />
        public MissingDependencyException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}