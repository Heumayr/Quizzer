using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Server;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>„Daten von diesem Rechner entfernen" (M28).</summary>
    public partial class SettingsViewModel
    {
        /// <summary>Wo Quizzer auf diesem Rechner ablegt - die Zeile über dem Knopf.</summary>
        public string ProgramDataOrt => Settings.ProgramDataRoot;

        private AsyncRelayCommand? removeDataCommand;

        /// <summary>
        /// Entfernt alles, was Quizzer auf diesem Rechner abgelegt hat, und startet neu.
        /// <para>
        /// <b>Nutzerwort vom 2026-10-23 (M28):</b> „mann soll aber seine daten mit einem mal aus den
        /// programdata entfernen können" - gewählt: der Knopf steht immer hier, auch ohne Server.
        /// Dort ist es die einzige Kopie; die Rückfrage sagt das ausdrücklich.
        /// </para>
        /// </summary>
        public ICommand RemoveDataCommand => removeDataCommand ??= new AsyncRelayCommand(async _ =>
        {
            var ziele = LocalDataRemoval.Current();
            var offen = await ServerSession.FlushAsync();

            if (!UserPrompt.Confirm(RemovalQuestion(ziele, offen), "Daten entfernen"))
                return;

            if (ServerSession.IsActive)
                await ServerSession.SignOutAsync();

            var fehler = await LocalDataRemoval.RemoveAsync(ziele);

            if (fehler.Count > 0)
            {
                UserPrompt.Inform(
                    "Nicht alles ließ sich entfernen - meist, weil eine Datei noch geöffnet war:"
                    + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, fehler.Take(10)),
                    "Daten entfernen");
            }

            AppRestart.Restart();
        });

        private static string RemovalQuestion(LocalDataRemoval.Targets ziele, int offen)
        {
            var absaetze = new List<string>
            {
                $"Entfernt wird alles, was Quizzer auf diesem Rechner unter {ziele.ProgramDataRoot} abgelegt hat "
                + "- Bilder, Töne und Designs -, dazu die Zwischendatenbanken und gemerkten Anmeldungen des "
                + "Servermodus. Danach startet das Programm neu.",
            };

            if (ServerSession.IsOffline)
            {
                absaetze.Add("Du arbeitest ohne Server: was seit der letzten Verbindung gespeichert wurde, geht dabei verloren.");
            }
            else if (ServerSession.IsActive)
            {
                absaetze.Add(offen > 0
                    ? $"{offen} Änderung(en) sind noch nicht beim Server und gehen dabei verloren. Alles andere bleibt auf dem Server; die nächste Anmeldung holt es wieder."
                    : "Auf dem Server bleibt alles; die nächste Anmeldung holt es wieder.");
            }
            else
            {
                absaetze.Add("Ohne Server ist das die einzige Kopie dieser Dateien - sie lassen sich nicht wiederherstellen. "
                    + "Spiele und Fragen in der lokalen Datenbank bleiben, ihre Bilder und Töne nicht.");
            }

            var eigener = ServerSession.LocalConnectionString == null ? Settings.FilePathQuizzer : null;
            if (!string.IsNullOrWhiteSpace(eigener) && !Settings.IsUnderProgramDataRoot(eigener))
                absaetze.Add($"Der eingestellte Datenordner {eigener} liegt nicht darunter und bleibt unberührt.");

            absaetze.Add("Wirklich entfernen?");
            return string.Join(Environment.NewLine + Environment.NewLine, absaetze);
        }
    }
}
