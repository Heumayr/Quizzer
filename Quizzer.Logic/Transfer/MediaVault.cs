using Quizzer.DataModels;
using System.Security.Cryptography;

namespace Quizzer.Logic.Transfer
{
    /// <summary>Wohin eine Mediendatei gehört.</summary>
    public enum MediaRoot
    {
        /// <summary>Die Medien der Fragen - <c>&lt;Datenordner&gt;\Resources</c>.</summary>
        Resources = 0,

        /// <summary>Die Texturen eines Designs - <c>&lt;Datenordner&gt;\Themes\&lt;Ordner&gt;</c>.</summary>
        Theme = 1,
    }

    /// <summary>
    /// Liest und schreibt Mediendateien - und ist die <b>einzige</b> Stelle, an der ein Pfad
    /// entsteht.
    /// <para>
    /// <b>Der Grund ist der Import.</b> Ein Bündel kommt aus fremder Hand; darin steht ein
    /// Dateiname, den jemand anders geschrieben hat. Würde er als Pfad benutzt, genügte
    /// <c>..\..\Windows\etwas.txt</c>, um beim Auspacken irgendwohin zu schreiben. Hier wird
    /// deshalb <b>jeder</b> Name auf seinen blanken Dateinamen zurückgeschnitten und der fertige
    /// Pfad danach noch einmal daraufhin geprüft, ob er wirklich unter der erlaubten Wurzel
    /// liegt.
    /// </para>
    /// <para>
    /// <b>Gleiche Inhalte bekommen denselben Namen.</b> Der Name entsteht aus einem Prüfwert über
    /// den Inhalt - ein zweiter Import derselben Datei legt sie deshalb nicht ein zweites Mal ab.
    /// </para>
    /// </summary>
    public interface IMediaVault
    {
        /// <summary>Liest eine Datei. <c>null</c>, wenn es sie nicht gibt.</summary>
        byte[]? Read(MediaRoot wurzel, string? ordner, string dateiname);

        /// <summary>
        /// Legt eine Datei ab und gibt den Namen zurück, unter dem sie nun liegt. Gibt es sie
        /// inhaltsgleich schon, wird nichts geschrieben und der vorhandene Name gemeldet.
        /// </summary>
        string Write(MediaRoot wurzel, string? ordner, string endung, byte[] inhalt);

        /// <summary>
        /// Legt eine Design-Textur unter ihrem <b>vorgeschriebenen</b> Namen ab.
        /// <para>
        /// <b>Warum es das braucht:</b> <see cref="Write"/> vergibt den Namen bewusst aus dem
        /// Pruefwert des Inhalts, damit eine fremde Datei den Zielnamen nicht bestimmen kann.
        /// Eine Textur muss aber exakt heissen wie in <c>ThemeAssets.Texturen</c> - sonst findet
        /// das Design sie nie.
        /// </para>
        /// <para>
        /// <b>Der Name kommt trotzdem nicht aus dem Buendel:</b> zugelassen sind ausschliesslich
        /// die bekannten Texturnamen. Alles andere wird abgewiesen.
        /// </para>
        /// </summary>
        /// <returns><c>true</c>, wenn geschrieben wurde.</returns>
        bool WriteTextur(string ordner, string dateiname, byte[] inhalt);
    }

    /// <summary>Die Umsetzung auf dem Dateisystem.</summary>
    public sealed class FileSystemMediaVault : IMediaVault
    {
        /// <summary>Endungen, die überhaupt abgelegt werden. Alles andere wird abgewiesen.</summary>
        private static readonly string[] ErlaubteEndungen =
        [
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".mp4", ".avi", ".mov", ".wmv", ".mkv",
            ".mp3", ".wav", ".ogg", ".flac",
            ".pdf", ".doc", ".docx", ".txt",
        ];

        /// <summary>Der Ordner einer Wurzel - und der einzige Ort, an dem er entsteht.</summary>
        public static string RootFolder(MediaRoot wurzel, string? ordner)
        {
            var basis = wurzel switch
            {
                MediaRoot.Theme => Path.Combine(Settings.FilePathQuizzer, "Themes"),
                _ => Settings.ResourceRootFolder,
            };

            if (wurzel != MediaRoot.Theme || string.IsNullOrWhiteSpace(ordner))
                return basis;

            // Auch der Ordnername kommt aus dem Buendel - er wird genauso beschnitten.
            return Path.Combine(basis, BlankerName(ordner));
        }

        /// <summary>
        /// Schneidet einen Namen aus fremder Hand auf seinen blanken Dateinamen zurück.
        /// <para>
        /// <b>Es sind drei Absicherungen, und das ist gemessen kein Übermaß.</b> Am 2026-09-06
        /// wurde jede einzeln ausgebaut: die Zusicherung blieb jedes Mal grün, weil eine der
        /// beiden anderen noch griff. Erst mit allen dreien ausgebaut landete die Datei
        /// außerhalb des Datenordners. Wer eine davon für überflüssig hält, hat die anderen
        /// beiden nicht gesehen.
        /// </para>
        /// <list type="number">
        ///   <item><description>der Pfadanteil fällt weg (<c>GetFileName</c>)</description></item>
        ///   <item><description>ein leerer Rest und <c>.</c>/<c>..</c> werden ersetzt</description></item>
        ///   <item><description>jedes unerlaubte Zeichen wird ersetzt - das fängt auch die
        ///   Trennzeichen, wenn die erste Stufe umgangen würde</description></item>
        /// </list>
        /// </summary>
        private static string BlankerName(string name)
        {
            // Bewusst ueber Path.DirectorySeparatorChar statt ueber ein
            // Backslash-Zeichenliteral: das ist die dokumentierte Falle des Projekts.
            var bereinigt = name
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .Replace(':', '_');

            var blank = Path.GetFileName(bereinigt);

            if (string.IsNullOrWhiteSpace(blank) || blank is "." or "..")
                return "datei";

            foreach (var c in Path.GetInvalidFileNameChars())
                blank = blank.Replace(c, '_');

            return blank;
        }

        /// <summary>
        /// Wirft, wenn der fertige Pfad die Wurzel verlässt.
        /// <para>
        /// Die letzte der drei Absicherungen - siehe <see cref="BlankerName"/>. Sie steht hier,
        /// weil eine Regel, die nur an einer Stelle greift, irgendwann an einer zweiten Stelle
        /// umgangen wird.
        /// </para>
        /// </summary>
        private static string PfadInWurzel(string wurzelOrdner, string dateiname)
        {
            var voll = Path.GetFullPath(Path.Combine(wurzelOrdner, BlankerName(dateiname)));
            var wurzel = Path.GetFullPath(wurzelOrdner);

            if (!voll.StartsWith(wurzel + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetDirectoryName(voll), wurzel, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Der Pfad '{voll}' liegt nicht unter '{wurzel}'.");

            return voll;
        }

        public byte[]? Read(MediaRoot wurzel, string? ordner, string dateiname)
        {
            if (string.IsNullOrWhiteSpace(dateiname))
                return null;

            var pfad = PfadInWurzel(RootFolder(wurzel, ordner), dateiname);

            return File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;
        }

        public bool WriteTextur(string ordner, string dateiname, byte[] inhalt)
        {
            if (string.IsNullOrWhiteSpace(ordner) || inhalt == null || inhalt.Length == 0)
                return false;

            // Der Name kommt NICHT aus dem Buendel, sondern aus der bekannten Liste - ein
            // Buendel kann damit keinen Zielnamen bestimmen.
            var bekannt = DataModels.Themes.ThemeAssets.Texturen
                .Any(t => string.Equals(t.Dateiname, dateiname, StringComparison.OrdinalIgnoreCase));

            if (!bekannt)
                return false;

            var ordnerPfad = RootFolder(MediaRoot.Theme, ordner);

            Directory.CreateDirectory(ordnerPfad);

            File.WriteAllBytes(PfadInWurzel(ordnerPfad, dateiname), inhalt);

            return true;
        }

        public string Write(MediaRoot wurzel, string? ordner, string endung, byte[] inhalt)
        {
            var saubereEndung = SaubereEndung(endung);
            var ordnerPfad = RootFolder(wurzel, ordner);

            Directory.CreateDirectory(ordnerPfad);

            // Der Name kommt aus dem Inhalt, nicht aus dem Buendel. Damit kann eine fremde Datei
            // den Zielnamen gar nicht bestimmen - und gleiche Inhalte liegen nur einmal da.
            var name = Pruefwert(inhalt) + saubereEndung;
            var pfad = PfadInWurzel(ordnerPfad, name);

            if (!File.Exists(pfad))
                File.WriteAllBytes(pfad, inhalt);

            return name;
        }

        /// <summary>Eine Endung, die abgelegt werden darf - sonst <c>.bin</c>.</summary>
        private static string SaubereEndung(string endung)
        {
            var e = (endung ?? string.Empty).Trim().ToLowerInvariant();

            if (!e.StartsWith('.'))
                e = "." + e;

            return ErlaubteEndungen.Contains(e) ? e : ".bin";
        }

        /// <summary>Die ersten 16 Zeichen eines SHA-256 über den Inhalt.</summary>
        private static string Pruefwert(byte[] inhalt)
            => Convert.ToHexStringLower(SHA256.HashData(inhalt))[..16];
    }
}
