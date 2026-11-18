using Quizzer.DataModels.Enumerations;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Quizzer.Base
{
    public abstract class ViewModelBase : ViewCommonBase, INotifyPropertyChanged
    {
        private Window? window;

        public EditResultState ResultState { get; set; } = EditResultState.None;

        //public event EventHandler? VMSaved;

        public Window? Window
        {
            get => window;
            set
            {
                if (!Equals(window, value))
                {
                    window?.Closed -= Window_Closed;
                    window?.Loaded -= Window_Loaded;
                    window?.SourceInitialized -= Window_SourceInitialized;
                    window?.ContentRendered -= Window_ContentRendered;
                    window = value;
                    window?.SourceInitialized += Window_SourceInitialized;
                    window?.ContentRendered += Window_ContentRendered;
                    window?.Loaded += Window_Loaded;
                    window?.Closed += Window_Closed;
                }
            }
        }

        private async void Window_ContentRendered(object? sender, EventArgs e)
        {
            try
            {
                await OnWindow_ContentRenderedAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        protected virtual async Task OnWindow_ContentRenderedAsync()
        {
        }

        private async void Window_SourceInitialized(object? sender, EventArgs e)
        {
            try
            {
                await OnWindow_SourceInitializedAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        protected virtual async Task OnWindow_SourceInitializedAsync()
        {
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await OnloadAsync();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        protected abstract Task OnloadAsync();

        protected virtual Task OnClosed()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Läuft, bis <see cref="OnClosed"/> fertig ist - etwa die beim Schließen gebuchte Bewertung (R5-3). Das Ereignis kehrt
        /// beim ersten <c>await</c> zurück und <c>ShowDialog</c> mit ihm; wer danach weiterrechnet, wartet hierauf.
        /// </summary>
        internal Task ClosedTask { get; private set; } = Task.CompletedTask;

        private void Window_Closed(object? sender, EventArgs e) => NotifyClosed();

        /// <summary>Was das Schließen des Fensters auslöst - ein Test ruft es ohne Fenster, mit demselben Wartegriff.</summary>
        internal Task NotifyClosed() => ClosedTask = RunOnClosedAsync();

        private async Task RunOnClosedAsync()
        {
            try
            {
                await OnClosed();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public virtual async Task InitializeAsync()
        {
        }

        /// <summary>
        /// Nur fuer Tests: startet <see cref="OnloadAsync"/> ohne Fenster. Im laufenden Programm
        /// haengt der Aufruf am <c>Loaded</c>-Ereignis des Fensters.
        /// </summary>
        internal Task LoadForTestAsync() => OnloadAsync();

        /// <summary>
        /// Nur fuer Tests: startet <see cref="OnClosed"/> ohne Fenster.
        /// </summary>
        internal Task ClosedForTestAsync() => OnClosed();

        public abstract Task VMSaveAsync();
    }
}