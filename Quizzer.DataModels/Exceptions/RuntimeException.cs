using System;
using System.Collections.Generic;
using System.Text;

namespace Quizzer.DataModels.Exceptions
{
    /// <summary>
    /// Represents errors that occur during application execution.
    /// </summary>
    [Serializable]
    public sealed class RuntimeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class.
        /// </summary>
        public RuntimeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public RuntimeException(string? message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public RuntimeException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        // private RuntimeException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        // {
        //     serializationInfo.AddValue(nameof(Message), Message);
        // }
    }
}