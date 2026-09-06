using Quizzer.DataModels;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.Logic.Demo
{
    /// <summary>
    /// Haengt dem Demo-Quizabend ein Bild und einen Ton an - Bilderrunde und Musikrunde.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06 (Frage F06).</b> Gemessen an der Spieldatenbank
    /// trugen die zwoelf Demofragen <b>null</b> Medien; alle sieben Medien im Bestand gehoerten
    /// den alten Erprobungsfragen in anderen Spielen. Damit fuhr der Abend, der zum
    /// Durchprobieren gedacht war, weder Bild noch Ton an - ausgerechnet das, was einen
    /// Quizabend ausmacht.
    /// </para>
    /// <para>
    /// <b>Es wird keine Datei kopiert oder erzeugt.</b> Angehaengt wird, was im
    /// Ressourcenordner ohnehin schon liegt; ist dort nichts Passendes, bleibt der Abend
    /// textlich und niemand erfaehrt einen Fehler. Inhaltlich passt das Medium nicht zur Frage -
    /// es geht um den Weg, nicht um den Inhalt.
    /// </para>
    /// </summary>
    public static partial class DemoDataSeeder
    {
        /// <summary>Die Frage, die die Bilderrunde vorfuehrt.</summary>
        private const string BildFrage = "Der weiße Hai";

        /// <summary>Die Frage, die die Musikrunde vorfuehrt.</summary>
        private const string TonFrage = "Das Instrument";

        private static readonly string[] BildEndungen = [".png", ".jpg", ".jpeg", ".bmp"];
        private static readonly string[] TonEndungen = [".mp3", ".wav"];

        /// <summary>
        /// Haengt der Frage ein Medium an, wenn sie eine der beiden vorgesehenen ist.
        /// <para>
        /// Das Medium landet auf dem <b>ersten normalen Schritt</b>, nicht auf dem Startschritt.
        /// Der Startschritt hat seine eigene Aufgabe: der Spielleiter liest die Frage vor, und
        /// wer zu frueh buzzert, darf sie nicht lesen koennen.
        /// </para>
        /// </summary>
        internal static void HaengeMediumAn(QuestionBase frage)
        {
            var istBild = frage.Designation.Contains(BildFrage, StringComparison.Ordinal);
            var istTon = frage.Designation.Contains(TonFrage, StringComparison.Ordinal);

            if (!istBild && !istTon)
                return;

            var datei = istBild ? SucheDatei(BildEndungen) : SucheDatei(TonEndungen);

            if (datei == null)
                return;

            var schritt = ErsterNormalerSchritt(frage);

            if (schritt == null)
                return;

            schritt.ResourceFileName = datei;
            schritt.ResourceTyp = istBild ? ResourceType.Image : ResourceType.Audio;
        }

        /// <summary>
        /// Der erste Schritt, der weder Start noch Abschluss ist - bei den Demofragen also der
        /// erste Hinweis.
        /// </summary>
        private static QuestionStepResource? ErsterNormalerSchritt(QuestionBase frage)
            => frage.Steps
                .Where(s => !s.IsStart && !s.IsFinish)
                .OrderBy(s => s.SequenceNumber)
                .FirstOrDefault();

        /// <summary>
        /// Der Dateiname der ersten passenden Datei im Ressourcenordner, sonst <c>null</c>.
        /// <para>
        /// Bewusst kein fest eingetragener Name: welche Dateien dort liegen, entscheidet der
        /// Nutzer, und ein erfundener Name ergaebe einen Schritt, der beim Abspielen schwarz
        /// bleibt - ohne Meldung, weil es im ganzen Programm keinen
        /// <c>MediaFailed</c>-Behandler gibt.
        /// </para>
        /// </summary>
        private static string? SucheDatei(string[] endungen)
        {
            var ordner = Settings.ResourceRootFolder;

            if (string.IsNullOrWhiteSpace(ordner) || !Directory.Exists(ordner))
                return null;

            return Directory.EnumerateFiles(ordner)
                .Where(p => endungen.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(Path.GetFileName)
                .FirstOrDefault();
        }
    }
}
