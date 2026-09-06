using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Oberflaechentexte sind deutsch (<c>standards-allgemein.md</c> §1), und ein Fenster traegt
    /// einen Titel, keinen Klassennamen.
    /// <para>
    /// Gemessen 2026-09-06: zehn Fenster standen mit "MainWindow", "GamesView" oder
    /// "QuestionSelectorView" in der Taskleiste, und fuenf Verwaltungsfenster waren vollstaendig
    /// englisch beschriftet - bis hin zu "Diff. x / +:" als einziger Erklaerung fuer die
    /// Punkteformel. Beides faellt beim Entwickeln nicht auf, weil man weiss, was gemeint ist.
    /// </para>
    /// </summary>
    [TestClass]
    public class GermanUserInterfaceUnitTests
    {
        /// <summary>
        /// Englische Beschriftungen, die hier vorkamen. Verglichen wird der <b>ganze</b>
        /// Attributwert - "Save" schlaegt an, "Savepoint speichern" nicht.
        /// </summary>
        private static readonly string[] EnglischeBeschriftungen =
        [
            "Save", "Save and Close", "Close", "Add", "Remove", "Select", "Deselect", "Delete",
            "Edit", "New", "Cancel", "OK Cancel", "Start Game", "Add Game", "Remove Game",
            "Add Player", "Remove Player", "Refresh Grid", "Enable Building", "Reset Game Build",
            "Edit Selected", "Designation", "Category", "Difficulty", "Points", "Minus-Points",
            "Final Score", "Correct Answered", "Display Name", "Resource Filename", "StepText",
            "Question", "Questions", "Categories", "Players", "Games", "Game",
            // Diese standen bis 2026-09-06 auf dem Beamer bzw. in der Medienvorschau.
            // "Not Supported" war die schlimmste Stelle: sie erschien vor den Gaesten und sah
            // aus wie ein Absturz.
            "Not Supported", "Start all", "Stop all", "Close all", "Stop",
        ];

        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis!.FullName;
        }

        private static List<string> XamlDateien(string root)
        {
            var pfad = Path.Combine(root, "Quizzer");

            return Directory.EnumerateFiles(pfad, "*.xaml", SearchOption.AllDirectories)
                .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToList();
        }

        /// <summary>
        /// Kein Fenster traegt den Namen seiner Datei als Titel.
        /// </summary>
        [TestMethod]
        public void NoWindowIsTitledAfterItsOwnClass()
        {
            var root = RepoRoot();
            var funde = new List<string>();

            foreach (var datei in XamlDateien(root))
            {
                var name = Path.GetFileNameWithoutExtension(datei);
                var inhalt = File.ReadAllText(datei);

                foreach (Match treffer in Regex.Matches(inhalt, "\\sTitle=\"([^\"{]+)\""))
                {
                    if (treffer.Groups[1].Value.Trim() == name)
                        funde.Add($"{Path.GetRelativePath(root, datei)}: Title=\"{name}\"");
                }
            }

            Assert.AreEqual(0, funde.Count,
                "Diese Fenster stehen mit ihrem Klassennamen in der Taskleiste: "
                + string.Join(", ", funde));
        }

        /// <summary>
        /// Keine Schaltflaeche und keine Spalte traegt eine englische Beschriftung.
        /// </summary>
        [TestMethod]
        public void NoLabelIsInEnglish()
        {
            var root = RepoRoot();
            var funde = new List<string>();

            foreach (var datei in XamlDateien(root))
            {
                var zeilen = File.ReadAllLines(datei);

                for (var i = 0; i < zeilen.Length; i++)
                {
                    if (zeilen[i].TrimStart().StartsWith("<!--"))
                        continue;

                    foreach (Match treffer in Regex.Matches(zeilen[i], "(Content|Header|ToolTip)=\"([^\"{]*)\""))
                    {
                        var wert = treffer.Groups[2].Value.Trim().TrimEnd(':');

                        if (EnglischeBeschriftungen.Contains(wert, StringComparer.Ordinal))
                            funde.Add($"{Path.GetRelativePath(root, datei)}:{i + 1} \"{wert}\"");
                    }
                }
            }

            Assert.AreEqual(0, funde.Count,
                "Oberflaechentexte sind deutsch (standards-allgemein.md §1). Englisch geblieben: "
                + string.Join(", ", funde));
        }

        /// <summary>
        /// <b>B55.</b> Jeder beschriftete Knopf der Abendfenster traegt eine Erklaerung.
        /// <para>
        /// Das Fragefenster ist die Maske, die der Spielleiter den ganzen Abend bedient, meist
        /// zum ersten Mal seit Wochen. Gemessen 2026-09-06: drei Knoepfe standen ohne
        /// Erklaerung da - "Zurueck (Ruecktaste)" zwischen zwei erklaerten, und die beiden
        /// Medienknoepfe "Abspielen"/"Anhalten", die es <b>nur</b> in diesem Fenster gibt
        /// (<c>ResourceViewerControl.SetMasterVisibility</c>).
        /// </para>
        /// <para>
        /// <b>Der Umfang ist am 2026-09-06 nachts gewachsen</b> - vom Fragefenster auf alles,
        /// was am Abend offen ist: Spielfeld, Ergebnisfenster samt Spielerkarten,
        /// Buzzer-Verwaltung und die Spieleliste. Warum nicht weiter, steht bei
        /// <see cref="AbendDateien"/>.
        /// </para>
        /// </summary>
        [TestMethod]
        public void EveryLabelledButtonOfTheEveningWindowsExplainsItself()
        {
            var root = RepoRoot();
            var funde = new List<string>();
            var geprueft = 0;

            foreach (var datei in AbendDateien(root))
            {
                var inhalt = File.ReadAllText(datei);

                foreach (Match treffer in Regex.Matches(inhalt, "<Button\\b(.*?)(/>|>)", RegexOptions.Singleline))
                {
                    var block = treffer.Groups[1].Value;

                    if (!block.Contains("Content=", StringComparison.Ordinal))
                        continue;

                    geprueft++;

                    if (block.Contains("ToolTip", StringComparison.Ordinal))
                        continue;

                    var name = Regex.Match(block, "Content=\"([^\"]*)\"");

                    funde.Add($"{Path.GetRelativePath(root, datei)}: \"{name.Groups[1].Value}\"");
                }
            }

            Assert.IsTrue(geprueft >= 25,
                $"Es wurden nur {geprueft} Knoepfe geprueft - die Suche greift nicht mehr, "
                + "und die Zusicherung waere gruen, ohne irgendetwas zu messen.");

            Assert.AreEqual(0, funde.Count,
                "Diese Knoepfe der Abendfenster stehen ohne Erklaerung da: "
                + string.Join(", ", funde));
        }

        /// <summary>
        /// Die Masken, die am Quizabend offen sind - Spielfeld, Fragefenster, Ergebnisfenster,
        /// Buzzer-Verwaltung und die Spieleliste, von der aus der Abend startet.
        /// <para>
        /// <b>Warum nicht alle Fenster.</b> Nachgemessen 2026-09-06: von 110 beschrifteten
        /// Knoepfen im Projekt trugen 67 keine Erklaerung. Ein Gate ueber alle waere sofort rot
        /// und damit abgeschaltet (<c>standards-allgemein.md</c> §5). Die Trennlinie ist nicht
        /// Bequemlichkeit, sondern der Zeitdruck: die Verwaltungsfenster bedient der Spielleiter
        /// in Ruhe, diese hier vor Gaesten.
        /// </para>
        /// </summary>
        private static List<string> AbendDateien(string root)
        {
            var dateien = new List<string>
            {
                Path.Combine(root, "Quizzer", "Views", "GamesView.xaml"),
            };

            foreach (var ordner in new[] { "GameViews", "BuzzerViews" })
            {
                dateien.AddRange(Directory.EnumerateFiles(
                    Path.Combine(root, "Quizzer", "Views", ordner), "*.xaml", SearchOption.AllDirectories));
            }

            foreach (var datei in dateien)
                Assert.IsTrue(File.Exists(datei), $"Nicht gefunden: {datei}");

            return dateien;
        }

        /// <summary>
        /// Die Warnfarbe steht nur auf Knoepfen, die etwas zerstoeren.
        /// <para>
        /// <c>DangerButton</c> ist rot. Stand sie auch auf harmlosen Knoepfen, gewoehnt sich der
        /// Spielleiter daran und uebersieht sie dort, wo sie zaehlt. Gemessen 2026-09-06: in der
        /// Kategorienverwaltung trug ausgerechnet <b>Speichern</b> die Warnfarbe.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheDangerStyleIsOnlyOnDestructiveButtons()
        {
            var root = RepoRoot();
            var funde = new List<string>();

            var zerstoerend = new[] { "entfernen", "löschen", "zurücksetzen", "verwerfen" };

            foreach (var datei in XamlDateien(root))
            {
                var inhalt = File.ReadAllText(datei);

                foreach (Match knopf in Regex.Matches(inhalt, "<Button.*?/>", RegexOptions.Singleline))
                {
                    if (!knopf.Value.Contains("DangerButton", StringComparison.Ordinal))
                        continue;

                    var beschriftung = Regex.Match(knopf.Value, "Content=\"([^\"{]*)\"");

                    if (!beschriftung.Success)
                        continue;

                    var text = beschriftung.Groups[1].Value;

                    if (!zerstoerend.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                        funde.Add($"{Path.GetRelativePath(root, datei)}: \"{text}\"");
                }
            }

            Assert.AreEqual(0, funde.Count,
                "Diese Knoepfe tragen die Warnfarbe, ohne etwas zu zerstoeren: "
                + string.Join(", ", funde));
        }

        /// <summary>
        /// Die Gegenrichtung: der Durchlauf liest wirklich Dateien und findet wirklich Titel.
        /// Ohne diese Probe waere gruen nicht von blind zu unterscheiden.
        /// </summary>
        [TestMethod]
        public void TheScanReadsRealWindows()
        {
            var root = RepoRoot();
            var dateien = XamlDateien(root);

            Assert.IsTrue(dateien.Count > 20,
                $"Nur {dateien.Count} XAML-Dateien gefunden - der Durchlauf greift ins Leere.");

            var mitTitel = dateien.Count(d =>
                Regex.IsMatch(File.ReadAllText(d), "\\sTitle=\"[^\"]+\""));

            Assert.IsTrue(mitTitel >= 10,
                $"Nur {mitTitel} Fenster mit Titel gefunden - das Muster greift nicht.");

            var mitBeschriftung = dateien.Count(d =>
                Regex.IsMatch(File.ReadAllText(d), "(Content|Header)=\"[^\"{]+\""));

            Assert.IsTrue(mitBeschriftung >= 10,
                $"Nur {mitBeschriftung} Dateien mit Beschriftungen - das Muster greift nicht.");
        }
    }
}
