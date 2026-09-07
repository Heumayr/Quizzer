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
                // "Game.Designation" als Pfad, nicht nameof: nameof(QuestionResult.Game.Designation)
                // ergibt nur "Designation" - sortiert wuerde dann nach einer Eigenschaft, die es
                // an QuestionResult gar nicht gibt.
                new System.ComponentModel.SortDescription(
                    "Game.Designation",
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

            // Ueber FilePicker, nicht ueber einen eigenen OpenFileDialog. Das war bis
            // 2026-09-07 die EINZIGE Stelle im Programm, die den Dialog selbst aufmachte -
            // und damit die einzige, die sich nicht pruefen liess: eine Zusicherung darauf
            // oeffnete im Testlauf ein echtes Dateifenster und blieb stehen. Gefunden genau
            // so, beim Versuch, diesen Weg abzusichern.
            //
            // Kein "Alle Dateien": eine Nicht-Bilddatei wurde erst in den Datenordner KOPIERT
            // und danach abgewiesen - sie blieb als Leiche liegen. Und eine unbekannte Endung
            // liess DetectResourceType werfen, also ein Fehlerfenster mit Stapelspur statt der
            // vorgesehenen Meldung.
            var quelle = FilePicker.AskForExistingFile(
                "Ressource auswählen",
                "Bilder|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp");

            if (string.IsNullOrWhiteSpace(quelle))
                return;

            // Geprueft wird an der QUELLE, vor dem Kopieren.
            if (!IstBild(quelle))
            {
                UserPrompt.Inform(
                    "Das ist keine Bilddatei: " + Path.GetFileName(quelle)
                    + Environment.NewLine + Environment.NewLine
                    + "Ein Mitspielerbild braucht PNG, JPG, BMP, GIF oder WEBP.",
                    "Bild wählen");

                return;
            }

            // Die Endung stimmt, der Inhalt kann trotzdem unlesbar sein - SkiaSharp wirft
            // dann, und ohne diesen Faenger stuende ein Fehlerfenster mit Stapelspur da.
            // Gemessen 2026-09-07: dieselbe Luecke wie beim Aufdeck-Bildwaehler.
            (string Filename, ResourceType Type) file;

            try
            {
                file = FileHelper.HandleSelectedResourceFile(
                    quelle, rootFolder, Player.Id.ToString(), true, true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException
                                          or UnauthorizedAccessException or NotSupportedException)
            {
                UserPrompt.Inform(ex.Message, "Bild wählen");

                return;
            }

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