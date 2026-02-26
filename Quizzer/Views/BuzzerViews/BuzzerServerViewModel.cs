using LocalBuzzer.Service;
using LocalBuzzer.Service.Base;
using Quizzer.Base;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.BuzzerViews
{
    public class BuzzerServerViewModel : ViewModelBase
    {
        private BuzzerServer? _server;

        public override Task VMSaveAsync()
        {
            return Task.CompletedTask;
        }

        public string State { get; set; } = "Stopped";

        private AsyncRelayCommand? startServerCommand;
        public ICommand StartServerCommand => startServerCommand ??= new AsyncRelayCommand(StartServerAsync);

        private async Task StartServerAsync(object? commandParameter)
        {
            if (_server != null)
            {
                MessageBox.Show("Server is already running");
                return;
            }

            State = "Starting";
            OnPropertyChanged(nameof(State));

            _server = new BuzzerServer();
            _server.WinnerDeclared += (name, round) =>
            {
                MessageBox.Show($"Winner: {name} (Runde {round})");
            };

            await _server.StartAsync(new BuzzerServerOptions
            {
                Port = 5000,
                WebRootPath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot")
            });

            State = "Running";
            OnPropertyChanged(nameof(State));
        }

        private AsyncRelayCommand? stopServerCommand;
        public ICommand StopServerCommand => stopServerCommand ??= new AsyncRelayCommand(StopServerAsync);

        private async Task StopServerAsync(object? commandParameter)
        {
            if (_server is not null) await _server.StopAsync();

            _server = null;
            State = "Stopped";
            OnPropertyChanged(nameof(State));
        }

        private AsyncRelayCommand? resetRoundCommand;
        public ICommand ResetRoundCommand => resetRoundCommand ??= new AsyncRelayCommand(ResetRoundAsync);

        private async Task ResetRoundAsync(object? commandParameter)
        {
            if (_server == null) return;

            await _server.ResetRoundAsync();
        }
    }
}