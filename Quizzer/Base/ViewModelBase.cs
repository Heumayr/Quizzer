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
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private Window? window;

        public Window? Window
        {
            get => window;
            set
            {
                if (!Equals(window, value))
                {
                    window?.Closed -= Window_Closed;
                    window?.Loaded -= Window_Loaded;
                    window = value;
                    window?.Loaded += Window_Loaded;
                    window?.Closed += Window_Closed;
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Onload();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        protected abstract Task Onload();

        protected virtual Task OnClosed()
        {
            return Task.CompletedTask;
        }

        private async void Window_Closed(object? sender, EventArgs e)
        {
            try
            {
                await VMSaveAsync();
                await OnClosed();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public virtual async Task InitializeAsync()
        {
        }

        public abstract Task VMSaveAsync();
    }
}