using Microsoft.Win32;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public class EditPlayerViewModel : ViewModelBase
    {
        public void SetPlayer(Player player)
        {
            Player = player;
        }

        private Player? _player;

        public Player? Player
        {
            get => _player;
            set
            {
                if (!Equals(_player, value))
                {
                    _player = value;
                    OnModelChanged();
                }
            }
        }

        protected override Task OnloadAsync()
        {
            return Task.CompletedTask;
        }

        public void OnModelChanged()
        {
            OnPropertyChanged(nameof(Player));
            OnDatagridSourceChanged();
        }

        private AsyncRelayCommand? saveCommand;

        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SaveAsync);

        private async Task SaveAsync(object? commandParameter)
        {
            await VMSaveAsync();
        }

        public override async Task VMSaveAsync()
        {
            if (Player == null) return;

            using var ctrl = new PlayersController();
            var result = await ctrl.UpsertAsync(Player);
            await ctrl.SaveChangesAsync();

            ResultState = result.Created ? EditResultState.New : EditResultState.Updated;
        }

        private AsyncRelayCommand? saveAndCloseCommand;
        public ICommand SaveAndCloseCommand => saveAndCloseCommand ??= new AsyncRelayCommand(SaveAndCloseAsync);

        private async Task SaveAndCloseAsync(object? param)
        {
            await SaveAsync(param);
            Window?.Close();
        }

        private void OnDatagridSourceChanged()
        {
            if (Player == null)
            {
                return;
            }

            var view = CollectionViewSource.GetDefaultView(Player.CurrentQuestionResults);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(
                new System.ComponentModel.SortDescription(
                    nameof(QuestionResult.Game.Designation),
                    System.ComponentModel.ListSortDirection.Ascending));

            view.Refresh();
        }

        private AsyncRelayCommand? selectResourceCommnad;
        public ICommand SelectResourceCommnad => selectResourceCommnad ??= new AsyncRelayCommand(PerformSelectResourceCommnadAsync);

        private async Task PerformSelectResourceCommnadAsync(object? commandParameter)
        {
            if (Player == null)
                return;

            if (Player.Id == Guid.Empty)
            {
                MessageBox.Show("Player must be saved before add a user picture!", "Save player");
                return;
            }

            var rootFolder = Settings.FilePathQuizzer;

            if (string.IsNullOrWhiteSpace(rootFolder))
                throw new InvalidOperationException("Root folder was not provided.");

            var dialog = new OpenFileDialog
            {
                Title = "Ressource auswählen",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                Filter =
                    "Bilder|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|" +
                    "Alle Dateien|*.*"
            };

            var result = dialog.ShowDialog();

            if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
                return;

            var file = FileHelper.HandleSelectedResourceFile(dialog.FileName, rootFolder, Player.Id.ToString());

            Player.UserPictureFileName = file.Filename;

            OnModelChanged();
        }
    }
}