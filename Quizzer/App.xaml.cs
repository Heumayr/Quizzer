using System.Configuration;
using System.Data;
using System.Windows;
using Quizzer.Base;
using Quizzer.Views;

namespace Quizzer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Settings.LoadSettings();

            var mainVm = new MainViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
        }
    }
}