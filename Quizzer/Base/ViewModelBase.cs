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
                    window = value;
                    window?.Closed += Window_Closed;
                }
            }
        }

        private async void Window_Closed(object? sender, EventArgs e)
        {
            try
            {
                await VMSaveAsync();
            }
            catch (Exception ex)
            {
                // TODO: Logging statt MessageBox, je nach App
                MessageBox.Show(ex.InnerException?.Message ?? ex.Message, "Save failed");
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