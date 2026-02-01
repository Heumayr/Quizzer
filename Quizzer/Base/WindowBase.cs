using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Quizzer.Base
{
    public class WindowBase : Window
    {
        public WindowBase()
        {
            Loaded += WindowBase_Loaded;
            DataContextChanged += WindowBase_DataContextChanged;
        }

        private void WindowBase_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ViewModelBase oldVm)
            {
                //oldVm.Dispose();
            }

            if (e.NewValue is ViewModelBase newVm)
            {
                newVm.Window = this;
            }
        }

        private async void WindowBase_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModelBase vm)
            {
                await vm.InitializeAsync();
            }
        }
    }
}