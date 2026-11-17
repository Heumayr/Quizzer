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

        /// <summary>
        /// M10: am Telefon selbst anmelden - Name waehlen oder Gast, warten, bis der
        /// Spielleiter bestaetigt, dann verbinden. Ein Kennwort geht dabei nie mit.
        /// </summary>
        [TestMethod]
        public void TheJoinFlowWaitsForTheGameMaster() => LaufeMit("join.test.mjs");

        /// <summary>
        /// V5: der Bildschirm bleibt wach - im http-WLAN über ein stummes Video, unter https über
        /// Wake Lock, und nach dem Zurückkommen aus dem Hintergrund neu.
        /// </summary>
        [TestMethod]
        public void ThePhoneScreenStaysAwake() => LaufeMit("wachbleiben.test.mjs");

        /// <summary>
        /// M35 B3: der Minispiel-Rahmen - Warten, Countdown, Ergebnis; ein wiederholter Stand startet
        /// die Uhr nicht neu, und Art und Durchgang gehen mit jeder Abgabe.
        /// </summary>
        [TestMethod]
        public void TheMinigameFrameHandsOverToTheKindModule() => LaufeMit("minispielRahmen.test.mjs");

        /// <summary>
        /// M35 B3: Reaktion - rot, lokal gruen mit Symbol und „JETZT!", Zeit ab dem Umschalten am
        /// Telefon, Tippen vor Gruen ist ein Fruehstart.
        /// </summary>
        [TestMethod]
        public void TheReactionModuleMeasuresOnThePhone() => LaufeMit("reaktion.test.mjs");

        /// <summary>
        /// M35 C1: Tippsprint - gezaehlt am Telefon, Zwischenstaende im Takt, Endstand nach der Dauer,
        /// nie ueber 20 Tipps je Sekunde, Weiterzaehlen nach dem Wiederverbinden.
        /// </summary>
        [TestMethod]
        public void TheTapSprintCountsOnThePhone() => LaufeMit("tippsprint.test.mjs");

        /// <summary>
        /// M35 C1: Stoppuhr - Start durch den Spieler, 3 s sichtbar, dann verdeckt; gemessen ab dem
        /// eigenen Start, Aufgeben ueber der Grenze.
        /// </summary>
        [TestMethod]
        public void TheStopwatchMeasuresOnThePhone() => LaufeMit("stoppuhr.test.mjs");

        /// <summary>M35 C2: Spielerwahl - je Mitspieler eine Taste, genau eine Wahl, gesperrt keine.</summary>
        [TestMethod]
        public void ThePlayerChoiceSendsOneChosenId() => LaufeMit("spielerwahl.test.mjs");

        /// <summary>
        /// M35 C2: Duell - Gegnerwahl, ein Zug Schere-Stein-Papier, der fremde Zug nur vom Server,
        /// die Reaktion aus B3 als zweite Disziplin.
        /// </summary>
        [TestMethod]
        public void TheDuelChoosesAnOpponentAndKeepsTheMoveSecret() => LaufeMit("duell.test.mjs");

        /// <summary>M35 C2: Raeuber und Waechter - Schild oder Opfer, genau eine Wahl, Aufloesung vom Server.</summary>
        [TestMethod]
        public void TheRobberChoosesShieldOrVictim() => LaufeMit("raeuber.test.mjs");

        /// <summary>M35 C2: Teilen oder Nehmen - Partnerwahl, eine Entscheidung, die vier Ausgaenge.</summary>
        [TestMethod]
        public void ShareOrTakeDecidesOnceAndShowsTheOutcome() => LaufeMit("teilen.test.mjs");

        /// <summary>M35 C4: Zocker - genau ein Ausstieg, der Absturzpunkt erst nach der Runde.</summary>
        [TestMethod]
        public void TheCrashCashesOutOnceAndLearnsTheCrashOnlyAfterwards() => LaufeMit("zocker.test.mjs");

        /// <summary>M35 C5: Kopfrechnen - Ziffernblock, eine Abgabe je Aufgabe, die naechste kommt vom Server.</summary>
        [TestMethod]
        public void MentalArithmeticSendsOneAnswerPerTask() => LaufeMit("kopfrechnen.test.mjs");

        /// <summary>M35 C5: Memory - das Feld zeigt nur, was der Server schickt; gefundene und die offene Karte fragen ihn nicht.</summary>
        [TestMethod]
        public void MemoryShowsOnlyWhatTheServerSent() => LaufeMit("memory.test.mjs");

        /// <summary>M35 C5: Blitzrunde - eine Antwort je Aussage, die naechste kommt vom Server, richtig/falsch erst nach der Runde.</summary>
        [TestMethod]
        public void LightningRoundSendsOneVerdictPerStatement() => LaufeMit("blitzrunde.test.mjs");

        /// <summary>M35 C5: Buchstabensalat - eine Abgabe je Versuch, der naechste Begriff kommt vom Server, auch wenn ein anderer loest.</summary>
        [TestMethod]
        public void WordScrambleSendsOneGuessAndFollowsTheServer() => LaufeMit("buchstabensalat.test.mjs");

        /// <summary>M35 C6: Erklaerbaer - nur das Telefon des Erklaerers zeigt den Begriff und sendet nie; die anderen raten.</summary>
        [TestMethod]
        public void ExplainItShowsTheTermOnlyToTheExplainer() => LaufeMit("baer.test.mjs");

        /// <summary>M35 C6: Bluff - ein Bluff je Abgabe, in Phase 2 die Nummern des Servers; Urheber und echte kennt das Telefon nicht.</summary>
        [TestMethod]
        public void BluffSendsOneBluffThenOneServerNumber() => LaufeMit("bluff.test.mjs");

        /// <summary>M35 C6: Heisse Kartoffel - nur der Halter urteilt und gibt weiter, je Tipp eine Abgabe mit der Nummer des Servers; keine Uhr am Telefon.</summary>
        [TestMethod]
        public void HotPotatoLetsOnlyTheHolderAnswerAndPassOnce() => LaufeMit("kartoffel.test.mjs");

        /// <summary>M35 C4: Gluecksrad - nur der Dreher waehlt ein Ziel, genau einmal; die Aufloesung vom Server.</summary>
        [TestMethod]
        public void TheWheelLetsOnlyTheSpinnerChooseOnce() => LaufeMit("gluecksrad.test.mjs");

        /// <summary>M35 C3: Sonderregel Einsatz - eine Zahl bis zur Obergrenze, genau einmal, die eigene kommt zurueck.</summary>
        [TestMethod]
        public void TheStakeIsAWholeNumberUpToTheLimitSentOnce() => LaufeMit("einsatz.test.mjs");

        /// <summary>M35 C3: Sonderregel Risiko - doppelt ja oder nein, genau einmal, als Satz.</summary>
        [TestMethod]
        public void TheRiskChoiceIsSentOnceAndShownAsText() => LaufeMit("risiko.test.mjs");

        /// <summary>M35 C3b: wer die Frage nicht beantworten darf, sieht den Satz statt des Layouts - und bekommt es zurueck.</summary>
        [TestMethod]
        public void AnExcludedPhoneSaysThatOthersAnswer() => LaufeMit("antwortsperre.test.mjs");

        /// <summary>M35 C3b: Auktion - das Modul des Einsatzes mit eigenen Texten, 0 heisst passen, genau einmal.</summary>
        [TestMethod]
        public void TheAuctionBidsSecretlyOnceAndZeroPasses() => LaufeMit("auktion.test.mjs");

        /// <summary>M35 C3b: Wette, Experte, Allianz - die Frage des Servers, genau eine Wahl, die eigene als Satz.</summary>
        [TestMethod]
        public void TheSpecialRuleChoiceSendsOneIdAndShowsItAsText() => LaufeMit("wahl.test.mjs");

        /// <summary>M35 C7b: Joker-Runde - nur eigene Joker, Sperre mit Ziel, genau einmal, prePhase im Rahmen, Ende fuer alle.</summary>
        [TestMethod]
        public void TheJokerRoundSendsOwnJokerOnceAndShowsWhoUsedWhat() => LaufeMit("joker.test.mjs");

        /// <summary>M37 T4: das Team am Telefon - Symbol und Name auf der Farbe aus der Palette, der Name nur als Text.</summary>
        [TestMethod]
        public void ThePhoneShowsItsTeamWithSymbolNameAndColor() => LaufeMit("team.test.mjs");
    }
}
