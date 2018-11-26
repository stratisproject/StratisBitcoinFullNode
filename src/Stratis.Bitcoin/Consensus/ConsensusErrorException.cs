using System;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// An exception that is used when consensus breaking errors are found.
    /// </summary>
    public class ConsensusErrorException : Exception
    {
        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusErrorException"/>.
        /// </summary>
        /// <param name="error">The error that triggered this exception.</param>
        public ConsensusErrorException(ConsensusError error) : base(error.Message)
        {
            this.ConsensusError = error;
        }

        /// <summary>The error that triggered this exception. </summary>
        public ConsensusError ConsensusError { get; private set; }
    }

    /// <summary>
    /// A consensus error that is used to specify different types of reasons a block does not confirm to the consensus rules.
    /// </summary>
    public class ConsensusError
    {
        /// <summary>
        /// The code representing this consensus error.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// A user friendly message to describe this error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// A method that will throw a <see cref="ConsensusErrorException"/> with the current consensus error.
        /// </summary>
        public void Throw()
        {
            throw new ConsensusErrorException(this);
        }

        /// <summary>
        /// Initialize a new instance of <see cref="ConsensusErrorException"/>.
        /// </summary>
        /// <param name="code">The error code that represents the current consensus breaking error.</param>
        /// <param name="message">A user friendly message to describe this error.</param>
        public ConsensusError(string code, string message)
        {
            Guard.NotEmpty(code, nameof(code));
            Guard.NotEmpty(message, nameof(message));

            this.Code = code;
            this.Message = message;
        }

        /// <inheritdoc />
        [NoTrace]
        public override bool Equals(object obj)
        {
            var item = obj as ConsensusError;

            return (item != null) && (this.Code.Equals(item.Code));
        }

        /// <summary>
        /// Compare two instances of <see cref="ConsensusError"/> are the same.
        /// </summary>
        /// <param name="a">first instance to compare.</param>
        /// <param name="b">Second instance to compare.</param>
        /// <returns><c>true</c> if bother instances are the same.</returns>
        [NoTrace]
        public static bool operator ==(ConsensusError a, ConsensusError b)
        {
            if (object.ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.Code == b.Code;
        }

        /// <summary>
        /// Compare two instances of <see cref="ConsensusError"/> are not the same.
        /// </summary>
        [NoTrace]
        public static bool operator !=(ConsensusError a, ConsensusError b)
        {
            return !(a == b);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Code.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0} : {1}", this.Code, this.Message);
        }
    }
}