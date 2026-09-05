using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Sichtbarer Text traegt echte Umlaute (<c>standards-allgemein.md</c> §1).
    /// <para>
    /// Anlass 2026-09-05/06: der Nutzer meldete rund 35 Stellen mit umgeschriebenen Umlauten in
    /// Beschriftungen, Meldungen und Auswahlnamen. Sie waren ueber acht Dateien verstreut und
    /// nur von Hand zu finden - beim ersten Durchgang blieben mehrere liegen, weil eine
    /// Stichprobe nicht misst, was sie nicht sieht. Diese Zusicherung ersetzt die Stichprobe.
    /// </para>
    /// <para>
    /// Bewusst kein Suchen nach jedem <c>ae</c>/<c>oe</c>/<c>ue</c>: "neue", "Dauer", "Adresse",
    /// "Question" und hunderte weitere sind richtig. Stattdessen eine Liste von Wortstaemmen,
    /// die im Deutschen praktisch nie ohne Umlaut vorkommen. <b>Die Liste ist erweiterbar</b> -
    /// wer eine weitere Umschrift findet, traegt den Stamm hier ein.
    /// </para>
    /// </summary>
    [TestClass]
    public class VisibleTextUmlautUnitTests
    {
        /// <summary>
        /// Wortstaemme, die als Umschrift eines Umlauts gelten. Gross-/Kleinschreibung egal.
        /// </summary>
        private static readonly string[] Umschriften =
        [
            "fuer", "ueber", "waehrend", "zurueck", "koenn", "moegl", "naechst", "loesch",
            "loesung", "hoechst", "duerf", "waehl", "waere", "muess", "fuehr", "gemaess",
            "maessig", "groesse", "hoehe", "laenge", "flaeche", "staerke", "schaetz", "pruef",
            "aender", "ungueltig", "gueltig", "zufaellig", "zaehl", "erklaer", "spaeter",
            "vertraeg", "beruehr", "oertlich", "verfuegb", "ausfuehr", "durchfuehr", "einfuehr",
            "uebertrag", "zusaetzlich", "taetig", "faellig", "haeufig", "laeuft", "gehoert",
            "benoetig", "wuensch", "erwuenscht", "moechte", "haett", "koennt", "waer",
            "schliessl", "urspruengl", "regelmaessig", "vollstaendig", "unvollstaendig",
            "abhaengig", "unabhaengig", "zugehoerig", "gaengig", "endgueltig", "vorlaeufig",
            "traegt", "schlaegt", "haelt", "faellt", "laesst", "verlaesst", "erhaelt",
            "naehe", "hoeh", "groess", "aehnlich", "unnoetig", "noetig", "moeglich",
        ];

        /// <summary>Die Ordner, in denen sichtbarer Text entsteht.</summary>
        private static readonly string[] Quellordner =
        [
            "Quizzer", "Quizzer.DataModels", "Quizzer.Logic", "LocalBuzzer.Service",
        ];

        /// <summary>
        /// Dateien, deren Zeichenketten kein Anzeigetext sind. <c>DatabaseInitializer</c> ist ein
        /// reiner Testhelfer und wirft Entwicklermeldungen; dort ist die Umschrift richtig.
        /// </summary>
        private static readonly string[] AusgenommeneDateien =
        [
            "DatabaseInitializer.cs",
        ];

        /// <summary>
        /// Zeilen, die in ein Protokoll schreiben statt auf den Bildschirm. Auch dort bleibt die
        /// Umschrift richtig - es ist kein Anzeigetext.
        /// </summary>
        private static readonly string[] ProtokollAufrufe =
        [
            "Debug.WriteLine", "Trace.Write", "_logger.", "Logger.Log", "Console.Write",
        ];

        /// <summary>Sucht die Projektwurzel am <c>Quizzer.slnx</c> daneben.</summary>
        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis,
                "Quizzer.slnx nicht gefunden - ohne Projektwurzel misst dieser Test nichts.");

            return verzeichnis!.FullName;
        }

        private static IEnumerable<string> Quelldateien(string root)
        {
            foreach (var ordner in Quellordner)
            {
                var pfad = Path.Combine(root, ordner);

                if (!Directory.Exists(pfad))
                    continue;

                foreach (var datei in Directory.EnumerateFiles(pfad, "*.*", SearchOption.AllDirectories))
                {
                    var endung = Path.GetExtension(datei);

                    if (endung is not (".cs" or ".xaml" or ".js" or ".html"))
                        continue;

                    if (datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        || datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        || datei.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}")
                        || datei.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
                        continue;

                    if (AusgenommeneDateien.Any(a => Path.GetFileName(datei) == a))
                        continue;

                    yield return datei;
                }
            }
        }

        /// <summary>
        /// Liest die Anzeigetexte einer Datei, Zeile fuer Zeile.
        /// <para>
        /// Ein Protokollaufruf wird ueber die ganze <b>Anweisung</b> ausgenommen, nicht nur ueber
        /// seine erste Zeile. Gemessen 2026-09-06: <c>UnsavedChangesWatch.LogToDebug</c> ruft
        /// <c>Debug.WriteLine(</c> in einer Zeile und uebergibt den Text zwei Zeilen tiefer -
        /// eine zeilenweise Ausnahme haette ihn faelschlich als Anzeigetext gemeldet.
        /// </para>
        /// </summary>
        private static IEnumerable<(int Zeile, string Text)> Anzeigetexte(string[] zeilen, bool istMarkup)
        {
            var offeneKlammern = 0;

            for (var i = 0; i < zeilen.Length; i++)
            {
                var zeile = zeilen[i];
                var roh = zeile.TrimStart();

                if (roh.StartsWith("//") || roh.StartsWith('*') || roh.StartsWith("/*") || roh.StartsWith("<!--"))
                    continue;

                if (offeneKlammern > 0)
                {
                    offeneKlammern += KlammerSaldo(zeile);
                    continue;
                }

                if (ProtokollAufrufe.Any(zeile.Contains))
                {
                    offeneKlammern = Math.Max(KlammerSaldo(zeile), 0);
                    continue;
                }

                foreach (Match treffer in Regex.Matches(zeile, "\"([^\"\\\\]{3,})\""))
                    yield return (i + 1, treffer.Groups[1].Value);

                if (istMarkup)
                {
                    foreach (Match treffer in Regex.Matches(zeile, ">([^<>{}\"']{3,})<"))
                        yield return (i + 1, treffer.Groups[1].Value);
                }
            }
        }

        /// <summary>
        /// Klammern auf minus Klammern zu - Zeichenketten vorher entfernt, damit eine Klammer im
        /// Text die Zaehlung nicht verschiebt.
        /// </summary>
        private static int KlammerSaldo(string zeile)
        {
            var ohneTexte = Regex.Replace(zeile, "\"[^\"\\\\]*\"", "\"\"");

            return ohneTexte.Count(c => c == '(') - ohneTexte.Count(c => c == ')');
        }

        /// <summary>
        /// Kein sichtbarer Text traegt einen umgeschriebenen Umlaut.
        /// </summary>
        [TestMethod]
        public void NoVisibleTextCarriesATransliteratedUmlaut()
        {
            var root = RepoRoot();
            var funde = new List<string>();

            foreach (var datei in Quelldateien(root))
            {
                var istMarkup = datei.EndsWith(".xaml") || datei.EndsWith(".html");

                foreach (var (zeile, text) in Anzeigetexte(File.ReadAllLines(datei), istMarkup))
                {
                    var stamm = Umschriften.FirstOrDefault(
                        s => text.Contains(s, StringComparison.OrdinalIgnoreCase));

                    if (stamm != null)
                        funde.Add($"{Path.GetRelativePath(root, datei)}:{zeile} [{stamm}] {text.Trim()}");
                }
            }

            Assert.AreEqual(0, funde.Count,
                "Sichtbarer Text muss echte Umlaute tragen (standards-allgemein.md §1). "
                + $"Gefunden:{Environment.NewLine}{string.Join(Environment.NewLine, funde)}");
        }

        /// <summary>
        /// Die Gegenrichtung: der Test darf nicht deshalb gruen sein, weil er nichts liest.
        /// Ohne diese Probe waere ein leerer Dateidurchlauf nicht von Sauberkeit zu
        /// unterscheiden - genau die Falle, die ihn wertlos machen wuerde.
        /// </summary>
        [TestMethod]
        public void TheScanActuallyReadsTheSources()
        {
            var root = RepoRoot();
            var dateien = Quelldateien(root).ToList();

            Assert.IsTrue(dateien.Count > 100,
                $"Nur {dateien.Count} Quelldateien gefunden - der Durchlauf greift ins Leere.");

            var mitText = dateien.Count(d =>
                Anzeigetexte(File.ReadAllLines(d), d.EndsWith(".xaml")).Any());

            Assert.IsTrue(mitText > 50,
                $"Nur {mitText} Dateien mit Zeichenketten - die Textzerlegung greift nicht.");

            // Und der Vergleich selbst muss anschlagen koennen.
            var probe = "Es duerfen hoechstens zwei sein.";

            Assert.IsTrue(Umschriften.Any(s => probe.Contains(s, StringComparison.OrdinalIgnoreCase)),
                "Der Vergleich findet nicht einmal eine bekannte Umschrift.");
        }
    }
}
