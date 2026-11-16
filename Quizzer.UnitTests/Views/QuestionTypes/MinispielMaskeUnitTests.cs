using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Views.QuestionTypes.Typed;
using System.Windows;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Die Maske des Minispiels (M35 B2): Art und Parameter ueber den Optionen, und eine frisch
    /// ausgefuellte Mehrheit laesst sich speichern.
    /// </summary>
    [TestClass]
    public class MinispielMaskeUnitTests
    {
        private static (Quizzer.Views.QuestionTypes.EditQuestionViewModel Vm, MinispielViewModel Maske) Neu()
        {
            var vm = ZeileneditorUnitTests.Mit(QuestionType.Minigame, f =>
            {
                f.DesignationShort = "M";
                f.CategoryId = Guid.NewGuid();
            });

            Assert.IsInstanceOfType<MinispielViewModel>(vm.Zeileneditor, "Das Minispiel bekommt nicht seine Maske.");

            return (vm, (MinispielViewModel)vm.Zeileneditor);
        }

        /// <summary>Die Maske zeigt Art und Spielweise - und schreibt sie in die Frage.</summary>
        [TestMethod]
        public void TheMaskShowsKindAndVariantAndWritesThem()
        {
            var (vm, maske) = Neu();

            Assert.AreSame(maske, maske.Zusatzmaske, "Ohne Zusatzmaske bleibt der Block für Art und Parameter leer.");
            Assert.AreEqual(MinigameKind.Majority, maske.Art?.Kind);
            Assert.AreEqual(Visibility.Visible, maske.VarianteVisibility, "Die Spielweise „Minderheit“ ist nicht wählbar.");

            maske.Variante = MehrheitEvaluator.MinorityVariant;

            Assert.AreEqual(MehrheitEvaluator.MinorityVariant, ((MinigameQuestion)vm.Question!).Variant);
            StringAssert.Contains(maske.Regelzeile, "kleinsten");
        }

        /// <summary>
        /// Die Auswahlliste der Spielweisen bleibt dieselbe Liste, und -1 wird keine Spielweise.
        /// <para>
        /// Eine neue Liste je Meldung setzte in WPF die Auswahl zurueck, und deren Index -1 kaeme ueber
        /// die TwoWay-Bindung als <c>Variant = -1</c> in die Frage - beim ersten Wechsel der Spielweise.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheVariantListStaysPutAndMinusOneIsNoVariant()
        {
            var (vm, maske) = Neu();
            var vorher = maske.Varianten;

            maske.Variante = MehrheitEvaluator.MinorityVariant;
            Assert.AreSame(vorher, maske.Varianten, "Die Spielweisen kommen als neue Liste - die Auswahl springt.");

            maske.Variante = -1;
            Assert.AreEqual(MehrheitEvaluator.MinorityVariant, ((MinigameQuestion)vm.Question!).Variant);
        }

        /// <summary>
        /// Eine frisch ausgefuellte Mehrheit ist speicherbar - mit einer Option nicht. Gemessen an
        /// <c>CanSave</c>, denn die Pruefung laeuft auf der Vorschau der Maske, nicht auf der Frage.
        /// </summary>
        [TestMethod]
        public void AFreshlyFilledMajorityCanBeSavedAndOneOptionCannot()
        {
            var (vm, maske) = Neu();

            maske.Zeilen[0].Text = "Pizza";

            Assert.IsFalse(vm.CanSave, "Eine Mehrheit mit einer einzigen Option ist speicherbar.");
            Assert.IsTrue(vm.Issues.Any(i => i.Code == MinigameChecks.TooFewOptions),
                "Die Maske nennt nicht, was fehlt: " + string.Join(" | ", vm.Issues.Select(i => i.Code)));

            maske.Zeilen[1].Text = "Pasta";

            Assert.IsTrue(vm.CanSave,
                "Mit zwei Optionen nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));
        }

        /// <summary>
        /// M35 B3: die Reaktion in der Maske - Runden statt Zeilen, Layout aus der Art, ohne Optionen
        /// speicherbar. Leere Zeilen verschwinden; stehen noch Reste der Mehrheit darin, bleibt die Liste.
        /// </summary>
        [TestMethod]
        public void TheReactionAsksForRoundsInsteadOfRows()
        {
            var (vm, maske) = Neu();

            maske.Art = MinigameProfiles.For(MinigameKind.Reaction);
            var frage = (MinigameQuestion)vm.Question!;

            Assert.AreEqual(MinigameKind.Reaction, frage.Kind);
            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Der Wechsel der Art stellt das Telefon nicht um.");
            Assert.AreEqual(Visibility.Visible, maske.RundenVisibility, "Die Runden sind nicht einstellbar.");
            Assert.AreEqual(Visibility.Collapsed, maske.ZeilenVisibility, "Leere Zeilen stehen da, obwohl die Reaktion keine braucht.");
            StringAssert.Contains(maske.Zeilenhinweis, "keine Zeilen");
            StringAssert.Contains(vm.BuzzerLayoutText, "Minispiel");

            maske.Runden = 5;
            Assert.AreEqual(5, frage.Rounds);
            Assert.IsTrue(vm.CanSave, "Ohne Optionen nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));

            maske.Runden = 0;
            Assert.IsFalse(vm.CanSave, "Null Runden sind speicherbar.");

            var (vm2, mehrheit) = Neu();
            mehrheit.Zeilen[0].Text = "Pizza";
            mehrheit.Art = MinigameProfiles.For(MinigameKind.Reaction);
            Assert.AreEqual(Visibility.Visible, mehrheit.ZeilenVisibility, "Reste der Mehrheit sind nicht mehr zu entfernen.");
            StringAssert.Contains(mehrheit.Zeilenhinweis, "nicht gespielt");
            Assert.IsNotNull(vm2);
        }

        /// <summary>
        /// M35 C1: Tippsprint und Stoppuhr fragen nach Sekunden statt Runden; dieselbe Spalte heisst bei der
        /// Stoppuhr „Zielzeit". Ausserhalb der Grenzen nicht speicherbar.
        /// </summary>
        [TestMethod]
        [DataRow(MinigameKind.TapSprint, "Dauer (Sekunden)", 2)]
        [DataRow(MinigameKind.Stopwatch, "Zielzeit (Sekunden)", 4)]
        [DataRow(MinigameKind.MentalArithmetic, "Dauer (Sekunden)", 14)]
        [DataRow(MinigameKind.Memory, "Höchstdauer (Sekunden)", 19)]
        public void TheTimedKindsAskForSecondsNamedByTheKind(MinigameKind art, string beschriftung, int zuKurz)
        {
            var (vm, maske) = Neu();

            maske.Art = MinigameProfiles.For(art);
            var frage = (MinigameQuestion)vm.Question!;

            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout, "Der Wechsel der Art stellt das Telefon nicht um.");
            Assert.AreEqual(Visibility.Visible, maske.DauerVisibility, "Die Sekunden sind nicht einstellbar.");
            Assert.AreEqual(Visibility.Collapsed, maske.RundenVisibility, "Eine Art mit einer Runde fragt nach Runden.");
            Assert.AreEqual(beschriftung, maske.DauerBeschriftung);
            Assert.IsFalse(string.IsNullOrWhiteSpace(maske.DauerErklaerung), "Das Feld hat keine Erklaerung.");
            StringAssert.Contains(maske.Regelzeile, $"{frage.DurationSeconds} Sekunden", "Am Beamer fehlt die Zahl.");
            Assert.IsTrue(vm.CanSave, "Mit den Vorgaben nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));

            maske.Dauer = zuKurz;
            Assert.AreEqual(zuKurz, frage.DurationSeconds);
            Assert.IsFalse(vm.CanSave, $"{zuKurz} Sekunden sind speicherbar.");
        }

        /// <summary>
        /// M35 C5: die Blitzrunde nennt ihre Zeilen „Aussagen" und zeigt je Zeile den Haken „wahr"; mit weniger als
        /// vier Aussagen nicht speicherbar. Zurueck zur Mehrheit verschwindet der Haken wieder.
        /// </summary>
        [TestMethod]
        public void TheLightningRoundAsksForStatementsWithATruthTick()
        {
            var (vm, maske) = Neu();
            Assert.AreEqual(Visibility.Collapsed, maske.RichtigVisibility, "Die Mehrheit zeigt einen Haken.");

            maske.Art = MinigameProfiles.For(MinigameKind.LightningRound);

            Assert.AreEqual("Aussagen (Haken = wahr)", maske.ZeilenTitel);
            Assert.AreEqual(Visibility.Visible, maske.RichtigVisibility, "Der Haken „wahr“ fehlt.");
            Assert.AreEqual("wahr", maske.RichtigBeschriftung);
            Assert.AreEqual(Visibility.Visible, maske.DauerVisibility, "Die Sekunden sind nicht einstellbar.");
            Assert.IsFalse(vm.CanSave, "Eine Blitzrunde ohne Aussagen ist speicherbar.");

            maske.AddRowCommand.Execute(null);
            string[] aussagen = ["Der Mond ist ein Planet.", "Berlin liegt an der Spree.", "Spinnen haben acht Beine.", "Eis ist schwerer als Wasser."];
            for (var i = 0; i < aussagen.Length; i++)
                maske.Zeilen[i].Text = aussagen[i];
            maske.Zeilen[1].IstRichtig = true;

            StringAssert.Contains(maske.Zeilenhinweis, "4 Aussagen, davon 1 wahr");
            Assert.IsTrue(vm.CanSave, "Mit vier Aussagen nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));

            maske.Art = MinigameProfiles.For(MinigameKind.Majority);
            Assert.AreEqual(Visibility.Collapsed, maske.RichtigVisibility, "Nach dem Wechsel zur Mehrheit steht der Haken noch da.");
            Assert.AreEqual("Antwortoptionen", maske.ZeilenTitel);
        }

        /// <summary>
        /// M35 C2: Duell fragt nach Runden, Spielweise und Anteil, Raeuber nur nach dem Anteil, Teilen nach
        /// nichts. Ein Anteil von 0 ist nicht speicherbar, wo die Art ihn liest.
        /// </summary>
        [TestMethod]
        [DataRow(MinigameKind.Duel, true, true, true)]
        [DataRow(MinigameKind.RobberAndGuard, false, true, false)]
        [DataRow(MinigameKind.ShareOrTake, false, false, false)]
        [DataRow(MinigameKind.Crash, false, true, true)]
        [DataRow(MinigameKind.WheelOfFortune, false, true, false)]
        public void ThePlayerChoiceKindsAskForWhatTheyRead(MinigameKind art, bool runden, bool anteil, bool spielweise)
        {
            var (vm, maske) = Neu();

            maske.Art = MinigameProfiles.For(art);
            var frage = (MinigameQuestion)vm.Question!;

            Visibility Soll(bool sichtbar) => sichtbar ? Visibility.Visible : Visibility.Collapsed;

            Assert.AreEqual(BuzzerControlsLayout.Minigame, frage.BuzzerControlsLayout);
            Assert.AreEqual(Soll(runden), maske.RundenVisibility, "Runden");
            Assert.AreEqual(Soll(anteil), maske.AnteilVisibility, "Anteil");
            Assert.AreEqual(Soll(spielweise), maske.VarianteVisibility, "Spielweise");
            Assert.AreEqual(Visibility.Collapsed, maske.DauerVisibility, "Eine Art ohne Uhr fragt nach Sekunden.");
            Assert.IsTrue(vm.CanSave, "Mit den Vorgaben nicht speicherbar. Offen: "
                + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));

            maske.Anteil = 0;
            Assert.AreEqual(!anteil, vm.CanSave, anteil ? "Ein Anteil von 0 ist speicherbar." : "Ein ungelesener Anteil sperrt das Speichern.");
        }

        /// <summary>M35 C5: Kopfrechnen und Memory waehlen Schwierigkeit bzw. Feldgroesse ueber die Spielweise - drei Stufen, ohne Runden und Anteil.</summary>
        [TestMethod]
        [DataRow(MinigameKind.MentalArithmetic, 3)]
        [DataRow(MinigameKind.Memory, 3)]
        public void TheLevelKindsOfferTheirLevels(MinigameKind art, int stufen)
        {
            var (vm, maske) = Neu();

            maske.Art = MinigameProfiles.For(art);

            Assert.AreEqual(Visibility.Visible, maske.VarianteVisibility, "Die Stufe ist nicht waehlbar.");
            Assert.AreEqual(stufen, maske.Varianten.Count);
            Assert.AreEqual(Visibility.Collapsed, maske.RundenVisibility, "Runden");
            Assert.AreEqual(Visibility.Collapsed, maske.AnteilVisibility, "Anteil");
            Assert.IsTrue(vm.CanSave, "Mit den Vorgaben nicht speicherbar.");
        }
    }
}
