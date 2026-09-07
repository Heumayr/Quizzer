using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Die Telefonseite selbst - das, was auf den Geräten der Gäste läuft.
    /// <para>
    /// <b>Bis 2026-09-07 gab es dafür keine einzige automatische Prüfung.</b> Die vorhandenen
    /// Zusicherungen decken den Vertrag zwischen C# und JS ab (Feldnamen, Hub-Methoden), nicht
    /// aber das Verhalten der Layouts. Genau dort saß ein Fehler, der eine Runde unspielbar
    /// machen konnte: nach „Runde zurücksetzen" blieb ein Spieler, der schon abgegeben hatte,
    /// für immer gesperrt.
    /// </para>
    /// <para>
    /// <b>Die Prüfung läuft in Node über die echten Modul-Dateien</b>, mit einem
    /// Mindest-DOM - kein <c>jsdom</c>, weil das Projekt keine npm-Abhängigkeiten hat und eine
    /// aufzunehmen eine Entscheidung wäre, keine Testfrage.
    /// </para>
    /// <para>
    /// <b>Fehlt Node, wird das rot, nicht übersprungen.</b> Ein Test, der sich selbst abmeldet,
    /// sieht aus wie keiner - und dieser Rechner ist die einzige maschinelle Prüfung, die es
    /// gibt.
    /// </para>
    /// </summary>
    [TestClass]
    public class TelefonseiteUnitTests
    {
        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis!.FullName;
        }

        private static void LaufeMit(string skript)
        {
            var root = RepoRoot();
            var pfad = Path.Combine(root, "Quizzer.LogicUnitTests", "LocalBuzzer", "js", skript);

            Assert.IsTrue(File.Exists(pfad), $"Das Pruefskript fehlt: {pfad}");

            var start = new ProcessStartInfo("node", $"\"{pfad}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = root,
            };

            using var lauf = Process.Start(start);

            Assert.IsNotNull(lauf, "Node liess sich nicht starten.");

            var ausgabe = lauf!.StandardOutput.ReadToEnd();
            var fehler = lauf.StandardError.ReadToEnd();

            Assert.IsTrue(lauf.WaitForExit(60_000), "Der Node-Lauf haengt.");

            Assert.AreEqual(0, lauf.ExitCode,
                $"Die Telefonseite hat eine Zusicherung gerissen ({skript}):"
                + Environment.NewLine + fehler + ausgabe);

            StringAssert.Contains(ausgabe, "gruen",
                "Das Skript lief durch, ohne etwas zu melden - dann hat es womoeglich gar "
                + "nichts geprueft. Ausgabe: " + ausgabe);
        }

        /// <summary>
        /// Nach „Runde zurücksetzen" kann ein Spieler, der schon abgegeben hatte, wieder
        /// abgeben - und vorher nicht.
        /// </summary>
        [TestMethod]
        public void TheKeySelectLayoutSurvivesARoundReset() => LaufeMit("keySelectLayout.test.mjs");

        /// <summary>
        /// Die Schätzfrage: abgeben, den Tipp sehen, und nach „Runde zurücksetzen" von vorn.
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> nach dem Zurücksetzen stand weiter „Abgegeben: 42" im
        /// Feld, obwohl die Abgabe serverseitig gelöscht war.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheInputLayoutSurvivesARoundReset() => LaufeMit("inputLayout.test.mjs");

        /// <summary>
        /// Der Buzzer: ein zweiter Druck sendet nichts mehr, und die neue Runde geht wieder.
        /// <para>
        /// Die lokale Sperre <b>vor</b> dem Senden ist der Kern - ohne sie zählt eine Runde
        /// zwei Buzzer desselben Spielers.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheBuzzerLayoutLocksAfterOnePress() => LaufeMit("buzzerLayout.test.mjs");

        /// <summary>
        /// Der Layoutwechsel: er läuft, sobald der Spielleiter eine Frage eines anderen Typs
        /// öffnet. Klemmt er, bleibt das Telefon beim Layout der vorigen Frage stehen, und der
        /// Gast drückt ins Leere.
        /// <para>
        /// Geprüft wird auch die Gegenrichtung von <c>unlockAll</c>: ein Layout, das der Server
        /// gesperrt hält, darf dabei <b>nicht</b> freigegeben werden - sonst könnte ein Spieler
        /// in einer fremden Runde buzzern.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheLayoutManagerSwitchesCleanly() => LaufeMit("layoutManager.test.mjs");
    }
}
