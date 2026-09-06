using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Transfer;
using System.IO;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Ein Spiel weitergeben und eines annehmen.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „ich möchte einen export und import für ein spiel ...
    /// der import findet mit dem gesetzten moderator statt und die spieler können beim import
    /// gewählt werden (oder auch keine)".
    /// </para>
    /// </summary>
    public partial class GamesViewModel
    {
        private const string Dateifilter = "Quizzer-Bündel|*" + GameExporter.Extension + "|Alle Dateien|*.*";

        private static readonly FileSystemMediaVault Tresor = new();

        private AsyncRelayCommand? exportGameCommand;

        public ICommand ExportGameCommand => exportGameCommand ??= new AsyncRelayCommand(ExportAsync);

        private async Task ExportAsync(object? commandParameter)
        {
            var spiel = WelchesSpiel(commandParameter);

            if (spiel == null || spiel.Id == Guid.Empty)
            {
                UserPrompt.Inform("Bitte zuerst ein Spiel auswählen.", "Exportieren");
                return;
            }

            var ziel = FilePicker.AskForSaveTarget(
                "Spiel exportieren",
                SaubererDateiname(spiel.Designation) + GameExporter.Extension,
                Dateifilter);

            if (ziel == null)
                return;

            var ergebnis = await GameExporter.ExportAsync(spiel.Id, ziel, Tresor);

            var text = $"„{spiel.Designation}" + "“ wurde exportiert."
                + Environment.NewLine + Environment.NewLine
                + $"Fragen: {ergebnis.Fragen}" + Environment.NewLine
                + $"Medien: {ergebnis.Medien}" + Environment.NewLine
                + $"Größe: {ergebnis.Groesse / 1024.0 / 1024.0:0.0} MB";

            // Fehlende Medien werden GENANNT, nicht stillschweigend weggelassen - der Empfaenger
            // bekommt sie nicht, und der Absender soll es vorher wissen.
            if (ergebnis.FehlendeMedien.Count > 0)
            {
                text += Environment.NewLine + Environment.NewLine
                    + $"{ergebnis.FehlendeMedien.Count} Mediendatei(en) fehlen im Datenordner und "
                    + "sind deshalb nicht im Bündel:" + Environment.NewLine
                    + string.Join(Environment.NewLine, ergebnis.FehlendeMedien.Take(5));
            }

            UserPrompt.Inform(text, "Exportieren");
        }

        private AsyncRelayCommand? importGameCommand;

        public ICommand ImportGameCommand => importGameCommand ??= new AsyncRelayCommand(ImportAsync);

        private async Task ImportAsync(object? commandParameter)
        {
            var quelle = FilePicker.AskForExistingFile("Spiel importieren", Dateifilter);

            if (quelle == null)
                return;

            var mitspieler = ImportPlayerSelection.Ask();

            if (mitspieler == null)
                return;

            try
            {
                var ergebnis = await GameImporter.ImportAsync(
                    quelle, Tresor, Session.CurrentModeratorId, mitspieler);

                await OnloadAsync();

                UserPrompt.Inform(
                    $"„{ergebnis.Spiel.Designation}" + "“ wurde angelegt."
                    + Environment.NewLine + Environment.NewLine
                    + $"Fragen: {ergebnis.Fragen}" + Environment.NewLine
                    + $"Neue Kategorien: {ergebnis.NeueKategorien}" + Environment.NewLine
                    + $"Medien: {ergebnis.Medien}" + Environment.NewLine
                    + $"Mitspieler: {mitspieler.Count}",
                    "Importieren");
            }
            catch (InvalidDataException ex)
            {
                UserPrompt.Inform(
                    "Die Datei ließ sich nicht lesen:" + Environment.NewLine + Environment.NewLine
                    + ex.Message,
                    "Importieren");
            }
        }

        /// <summary>Ein Dateiname aus einer Bezeichnung - ohne alles, was Windows nicht mag.</summary>
        internal static string SaubererDateiname(string bezeichnung)
        {
            var name = bezeichnung;

            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            name = name.Trim();

            return string.IsNullOrWhiteSpace(name) ? "Spiel" : name;
        }
    }

    /// <summary>
    /// Die Mitspielerauswahl beim Import - gekapselt, damit das ViewModel kein Fenster selbst
    /// öffnet.
    /// </summary>
    public static class ImportPlayerSelection
    {
        /// <summary>
        /// Fragt nach den Mitspielern. Gibt eine leere Liste zurück, wenn keine gewählt wurden,
        /// und <c>null</c>, wenn abgebrochen wurde. <b>Beides ist nicht dasselbe</b> - „keine
        /// Mitspieler" ist eine gültige Wahl (Nutzerwort: „oder auch keine").
        /// </summary>
        public static Func<IReadOnlyList<Guid>?> Handler { get; set; } = Standard;

        public static IReadOnlyList<Guid>? Ask() => Handler();

        /// <summary>Setzt auf das echte Fenster zurueck.</summary>
        public static void Reset() => Handler = Standard;

        private static IReadOnlyList<Guid>? Standard()
        {
            var fenster = new ImportPlayersView();

            fenster.ShowDialog();

            return fenster.Gewaehlt;
        }
    }
}
