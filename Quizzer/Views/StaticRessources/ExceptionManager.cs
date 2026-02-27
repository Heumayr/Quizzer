using Quizzer.Base;
using Quizzer.Views.HelperViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Quizzer.Views.StaticRessources
{
    public static class ExceptionManager
    {
        public static void HandleException(Exception ex)
        {
            if (ex == null)
                return;

            var window = new Window();

            var contentPanel = new StackPanel();

            _ = RenderException(contentPanel, ex);

            contentPanel.Children.Add(new Button()
            {
                Content = "OK",
                Command = new RelayCommand((p) => window.Close())
            });

            window.Content = contentPanel;

            window.Show();
        }

        private static object RenderException(StackPanel pnl, Exception ex)
        {
            var view = new ExceptionView();
            view.Message.Content = ex.Message;
            view.Source.Content = ex.Source;
            pnl.Children.Add(view);

            if (ex.InnerException != null)
            {
                return RenderException(pnl, ex.InnerException);
            }

            return ex;
        }
    }
}