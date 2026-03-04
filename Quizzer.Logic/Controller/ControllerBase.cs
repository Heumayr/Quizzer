using Quizzer.Logic.Context;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Collections.Specialized.BitVector32;

namespace Quizzer.Logic.Controller
{
    /// <summary>
    /// Base class for all controllers. Implements the dispose pattern and authorization check. Grants access to the database.
    /// </summary>
    public abstract class ControllerBase : IDisposable
    {
        private bool disposedValue;

        private readonly bool owner;
        private string sessionToken = string.Empty;

        internal DataContext Context { get; set; }

        /// <summary>
        /// Creates a new data context.
        /// </summary>
        protected ControllerBase()
        {
            Context = new();
            owner = true;
        }

        /// <summary>
        /// Uses the database and authentication token from another controller.
        /// </summary>
        /// <param name="other">The other controller to copy the context and session token from.</param>
        protected ControllerBase(ControllerBase other)
        {
            Context = other.Context;
        }

        /// <summary>
        /// Disposes the resources used by the controller.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && owner)
                {
                    Context?.Dispose();
                }
                Context = null;

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes the controller.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}