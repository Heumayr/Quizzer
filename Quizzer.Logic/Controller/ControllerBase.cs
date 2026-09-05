using Microsoft.EntityFrameworkCore;
using Quizzer.Logic.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
                    ReportUnsavedChanges();
                    Context?.Dispose();
                }
                Context = null!;

                disposedValue = true;
            }
        }

        /// <summary>
        /// Meldet Aenderungen, die nie geschrieben wurden. Nur der Eigentuemer des Kontexts
        /// prueft - ein Controller, der sich den Kontext teilt, waere der falsche Melder.
        /// </summary>
        private void ReportUnsavedChanges()
        {
            try
            {
                if (Context == null)
                    return;

                var offen = Context.ChangeTracker.Entries().Count(e =>
                    e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

                UnsavedChangesWatch.Report(GetType().Name, offen);
            }
            catch (Exception ex)
            {
                // Dispose darf nicht werfen, und der Kontext kann hier schon halb abgebaut sein.
                Debug.WriteLine($"Pruefung auf ungespeicherte Aenderungen gescheitert: {ex.Message}");
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