using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.Logic.Vorrat;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Den Vorrat geteilter Minispiele anlegen (M35 C8, V4) - im Fragenkatalog, weil dort die Fragen stehen, die er
    /// anlegt, und weil man sie von hier aus gleich sieht.
    /// <para>
    /// <b>Im Servermodus wird vorher abgeholt.</b> Gemessen (<c>MinispielVorratTests</c>): wer den Knopf drückt, bevor
    /// er den Vorrat eines anderen Kontos abgeholt hat, legt jede Frage lokal mit sich als Besitzer an, und der Server
    /// weist jede davon als Konflikt ab. Nach dem Abholen sind die festen Ids da, und es entsteht nichts.
    /// </para>
    /// </summary>
    internal partial class QuestionsViewModel
    {
        private const string VorratTitel = "Minispiel-Vorrat";

        private AsyncRelayCommand? vorratAnlegenCommand;

        public ICommand VorratAnlegenCommand => vorratAnlegenCommand ??= new AsyncRelayCommand(VorratAnlegenAsync);

        private async Task VorratAnlegenAsync(object? _)
        {
            if (!await VorherAbholenAsync())
                return;

            var ergebnis = await MinispielVorrat.CreateAsync(Session.CurrentModeratorId);

            UserPrompt.Inform(VorratMeldung(ergebnis), VorratTitel);

            await OnloadAsync();
        }

        /// <summary>Was der Spielleiter danach liest - auch, dass nichts geschah.</summary>
        internal static string VorratMeldung(MinispielVorrat.Ergebnis ergebnis)
            => ergebnis.Neu == 0
                ? $"Der Vorrat ist schon vollständig da ({ergebnis.Vorhanden} Minispiele). Es wurde nichts geändert."
                : $"{ergebnis.Neu} Minispiele angelegt, Kategorie „{MinispielVorrat.KategorieName}“."
                  + (ergebnis.Vorhanden > 0 ? $" {ergebnis.Vorhanden} gab es schon, sie bleiben, wie sie sind." : string.Empty)
                  + Environment.NewLine + Environment.NewLine
                  + "Sie sind mit allen Konten geteilt: im Servermodus ziehen andere sie in ihr Raster, ändern kann sie nur, wer sie angelegt hat.";

        /// <summary>
        /// Holt im Servermodus den Stand des Servers, bevor angelegt wird. Ohne Server gibt es nichts abzuholen. Liegen
        /// abgewiesene Änderungen, ersetzt das Abholen sie durch den Serverstand - dann wird gefragt wie bei „Vom Server laden".
        /// </summary>
        private static async Task<bool> VorherAbholenAsync()
        {
            if (!Server.ServerSession.IsActive)
                return true;

            if (Server.ServerSession.IsOffline || Server.ServerSession.Sync is not { } sync)
            {
                UserPrompt.Inform("Ohne Verbindung zum Server lässt sich nicht prüfen, ob ein anderes Konto den Vorrat "
                    + "schon angelegt hat. Bitte erst wieder verbinden.", VorratTitel);
                return false;
            }

            if (sync.CountRejections() > 0 && !UserPrompt.Confirm(
                    "Vor dem Anlegen wird der Stand vom Server geladen. Was der Server abgewiesen hat, wird dabei durch "
                    + "seinen Stand ersetzt. Fortfahren?", VorratTitel))
                return false;

            try
            {
                await sync.PullAsync();
                Server.ServerSession.Background?.Refresh();
                return true;
            }
            catch (Exception ex) when (ex is Client.ServerUnreachableException or Client.ApiException)
            {
                UserPrompt.Inform(ex.Message, VorratTitel);
                return false;
            }
        }
    }
}
