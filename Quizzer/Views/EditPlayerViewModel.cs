using Microsoft.Win32;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Quizzer.Views
{
    public partial class EditPlayerViewModel : ViewModelBase
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

        /// <summary>
        /// Speichert den Mitspieler.
        /// <para>
        /// <b>Ohne Namen wird nicht gespeichert.</b> Bis 2026-09-07 liess sich ein Mitspieler
        /// ganz ohne Bezeichnung und Anzeigenamen anlegen; er stand danach als leere Zeile in
        /// der Liste und - weil leer alphabetisch zuerst kommt - <b>vorgewählt in der
        /// Anmeldung</b>.
        /// </para>
        /// </summary>
        public override async Task VMSaveAsync()
        {
            if (Player == null) return;

            if (string.IsNullOrWhiteSpace(Player.CalculatedDisplayName))
            {
                UserPrompt.Inform(
                    "Der Mitspieler braucht einen Namen." + Environment.NewLine + Environment.NewLine
                    + "Ohne ihn steht er als leere Zeile in der Liste und in der Anmeldung.",
                    "Mitspieler speichern");

                return;
            }

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
                UserPrompt.Inform("Der Mitspieler muss gespeichert sein, bevor ein Bild hinterlegt werden kann.", "Bild hinterlegen");
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
                // Kein "Alle Dateien" mehr: eine Nicht-Bilddatei wurde erst in den Datenordner
                // KOPIERT und danach abgewiesen - sie blieb als Leiche liegen. Und eine
                // unbekannte Endung liess DetectResourceType werfen, also ein Fehlerfenster mit
                // Stapelspur statt der vorgesehenen Meldung. Gemessen 2026-09-07.
                Filter = "Bilder|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp"
            };

            var result = dialog.ShowDialog();

            if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
                return;

            // Geprueft wird an der QUELLE, vor dem Kopieren.
            if (!IstBild(dialog.FileName))
            {
                UserPrompt.Inform(
                    "Das ist keine Bilddatei: " + Path.GetFileName(dialog.FileName)
                    + Environment.NewLine + Environment.NewLine
                    + "Ein Mitspielerbild braucht PNG, JPG, BMP, GIF oder WEBP.",
                    "Bild wählen");

                return;
            }

            var file = FileHelper.HandleSelectedResourceFile(dialog.FileName, rootFolder, Player.Id.ToString(), true, true);

            Player.UserPictureFileName = file.Filename;

            OnModelChanged();
        }

        /// <summary>
        /// Ob die Datei nach ihrer Endung ein Bild ist. Wirft nicht - <c>DetectResourceType</c>
        /// wirft bei einer unbekannten Endung, und das waere hier ein Fehlerfenster mit
        /// Stapelspur.
        /// </summary>
        internal static bool IstBild(string pfad)
        {
            try
            {
                return FileHelper.DetectResourceType(pfad) == ResourceType.Image;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
    }
}