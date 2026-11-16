using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.LogicUnitTests.DataModels.Minispiele
{
    /// <summary>
    /// Die Heisse Kartoffel (M35 C6) als reine Funktion: wer beim Knall haelt, −B, wer sie ihm zugeschoben hat, +B; beim
    /// Starthalter nur das Minus; gekappt wird nur das Minus; die Spieler bleiben getrennt. Dazu Tafel, Zuendbereich, Profil
    /// und Pruefung.
    /// </summary>
    [TestClass]
    public class KartoffelUnitTests
    {
        private const int Betrag = 150;

        private static readonly Guid Anna = Guid.NewGuid();
        private static readonly Guid Bert = Guid.NewGuid();
        private static readonly Guid Carl = Guid.NewGuid();

        private static readonly (Guid Id, string Name)[] Spieler = [(Anna, "Anna"), (Bert, "Bert"), (Carl, "Carl")];

        private static string Name(Guid id) => Spieler.Single(s => s.Id == id).Name;

        [TestMethod]
        public void TheHolderLosesAndThePasserWinsTheShare()
        {
            var e = KartoffelEvaluator.Werte(new(Bert, Anna), Betrag, Kontostaende.OhneGrenze, Name);

            Assert.AreEqual(-Betrag, e.PunkteVon(Bert));
            Assert.AreEqual(Betrag, e.PunkteVon(Anna));
            Assert.IsFalse(e.Vorschlaege.ContainsKey(Carl), "Wer nichts damit zu tun hatte, bekommt einen Vorschlag.");
            Assert.AreEqual("Heiße Kartoffel: beim Knall gehalten – von Anna zugeschoben −150", e.Vorschlaege[Bert].Grund);
            Assert.AreEqual("Heiße Kartoffel: rechtzeitig an Bert weitergegeben +150", e.Vorschlaege[Anna].Grund);
        }

        /// <summary>Starthalter (oder vom Spielleiter hingelegt): niemand hat zugeschoben - nur das Minus, kein Plus fuer irgendwen.</summary>
        [TestMethod]
        public void TheStartHolderOnlyLoses()
        {
            var e = KartoffelEvaluator.Werte(new(Anna, null), Betrag, Kontostaende.OhneGrenze, Name);

            Assert.AreEqual(-Betrag, e.PunkteVon(Anna));
            Assert.AreEqual(1, e.Vorschlaege.Count, "Beim Starthalter bekommt jemand ein Plus.");
            Assert.AreEqual("Heiße Kartoffel: beim Knall gehalten −150", e.Vorschlaege[Anna].Grund);
        }

        /// <summary>E2 kappt nur das Minus am Kontostand - das Plus bleibt, auch wenn der Zuschieber selbst unter null steht.</summary>
        [TestMethod]
        public void OnlyTheMinusIsCapped()
        {
            var konten = new Kontostaende(new Dictionary<Guid, int> { [Bert] = 40, [Anna] = -500 }, UnterNullErlaubt: false);

            var e = KartoffelEvaluator.Werte(new(Bert, Anna), Betrag, konten, Name);

            Assert.AreEqual(-40, e.PunkteVon(Bert), "Das Minus wird nicht am Kontostand gekappt.");
            StringAssert.EndsWith(e.Vorschlaege[Bert].Grund, Punkteanteil.GekapptText);
            Assert.AreEqual(Betrag, e.PunkteVon(Anna), "Das Plus wird gekappt.");
            Assert.IsFalse(e.Vorschlaege[Anna].Grund.Contains("gekappt"));

            var erlaubt = KartoffelEvaluator.Werte(new(Bert, Anna), Betrag, konten with { UnterNullErlaubt = true }, Name);
            Assert.AreEqual(-Betrag, erlaubt.PunkteVon(Bert), "Mit erlaubtem Minus wird trotzdem gekappt.");
        }

        /// <summary>Halter und Zuschieber bleiben getrennt - auch dann, wenn einer von beiden schon einen Vorschlag haette.</summary>
        [TestMethod]
        public void HolderAndPasserStaySeparate()
        {
            var e = KartoffelEvaluator.Werte(new(Carl, Bert), Betrag, Kontostaende.OhneGrenze, Name);

            CollectionAssert.AreEquivalent(new[] { Carl, Bert }, e.Vorschlaege.Keys.ToArray());
            Assert.AreEqual(0, e.Vorschlaege.Values.Sum(v => v.Punkte), "Plus und Minus heben sich nicht auf - jemand bekommt doppelt.");

            var selbst = KartoffelEvaluator.Werte(new(Anna, Anna), Betrag, Kontostaende.OhneGrenze, Name);
            Assert.AreEqual(-Betrag, selbst.PunkteVon(Anna), "Wer sie sich selbst zuschiebt, verrechnet Plus und Minus.");
        }

        [TestMethod]
        public void WithoutABangNothingMoves()
        {
            Assert.AreEqual(0, KartoffelEvaluator.Werte(null, Betrag, Kontostaende.OhneGrenze, Name).Vorschlaege.Count);
            Assert.AreEqual(0, KartoffelEvaluator.Werte(new(Anna, Bert), 0, Kontostaende.OhneGrenze, Name).Vorschlaege.Count, "Ohne Betrag gibt es Vorschlaege.");
        }

        [TestMethod]
        [DataRow(60, 20, 60)]
        [DataRow(20, 20.0 / 3, 20)]
        public void TheFuseBurnsBetweenAThirdAndTheWholeTime(int sekunden, double kuerzeste, int laengste)
        {
            var (von, bis) = KartoffelEvaluator.Zuendbereich(sekunden);

            Assert.AreEqual(kuerzeste, von.TotalSeconds, 0.001);
            Assert.AreEqual(laengste, bis.TotalSeconds, 0.001);
        }

        [TestMethod]
        public void TheBoardShowsTheHolderAndAfterTheBangThePoints()
        {
            var laufend = KartoffelTafel.Laufend(Bert, new Dictionary<Guid, int> { [Anna] = 2 }, Spieler);

            Assert.AreEqual("Bert", laufend[0].Name, "Der Halter steht nicht vorn.");
            Assert.AreEqual("hält die Kartoffel!", laufend[0].Zeit);
            Assert.AreEqual("2× weitergegeben", laufend.Single(z => z.Name == "Anna").Zeit);
            Assert.IsFalse(laufend.Any(z => z.Sieger), "Waehrend der Runde ist jemand Sieger.");
            StringAssert.Contains(KartoffelTafel.Kopfzeile("Bert"), "Bert hält sie");

            var e = KartoffelEvaluator.Werte(new(Bert, Anna), Betrag, Kontostaende.OhneGrenze, Name);
            var ende = KartoffelTafel.Ende(e, Spieler);
            Assert.AreEqual("Anna", ende[0].Name);
            Assert.IsTrue(ende[0].Sieger, "Wer zugeschoben hat, ist nicht markiert.");
            Assert.AreEqual("beim Knall gehalten – von Anna zugeschoben – −150", ende.Single(z => z.Name == "Bert").Zeit);
            Assert.AreEqual("nicht betroffen – ±0", ende.Single(z => z.Name == "Carl").Zeit);
        }

        [TestMethod]
        public void TheResolutionNamesTruthHolderAndOutcome()
        {
            var verlauf = new List<KartoffelZug>
            {
                new(Anna, "Der Mond ist ein Planet.", false, false),
                new(Anna, "Spinnen haben acht Beine.", true, true, Bert),
                new(Bert, "Wale sind Säugetiere.", true, true),
                new(Bert, "Der Rhein mündet ins Schwarze Meer.", false),
            };

            CollectionAssert.AreEqual(new[]
            {
                "✗ falsch – Der Mond ist ein Planet. · Anna: daneben",
                "✓ wahr – Spinnen haben acht Beine. · Anna: richtig, weiter an Bert",
                "✓ wahr – Wale sind Säugetiere. · Bert: richtig, aber nicht mehr weitergegeben",
                "✗ falsch – Der Rhein mündet ins Schwarze Meer. · Bert: offen beim Knall",
            }, KartoffelTafel.Aufloesung(verlauf, Name).ToArray());

            Assert.AreEqual("BUMM nach 41 s! Die Kartoffel ist bei Bert geplatzt", KartoffelEvaluator.Ausgang(new(new(Bert, Anna), verlauf, 41), Name));
            StringAssert.Contains(KartoffelEvaluator.Ausgang(new(null, verlauf, null), Name), "ohne Knall");
        }

        [TestMethod]
        public void TheProfileLetsTheMasterPickTheFirstHolderAndNeedsTwoPlayers()
        {
            var profil = MinigameProfiles.For(MinigameKind.HotPotato);

            Assert.AreEqual(2, profil.MinPlayers);
            Assert.IsTrue(profil.ChooserOnly && profil.ShowsPairChoice, "Der Spielleiter kann nicht festlegen, wer zuerst haelt.");
            Assert.IsFalse(profil.ChoosesPartner);
            Assert.AreEqual("Wer hält zuerst", profil.ChooserLabel);
            Assert.IsTrue(profil.ShowTruePerRow, "Der Haken „wahr“ fehlt in der Maske.");
            Assert.IsTrue(profil.ShowShare && profil.ShowDurationSeconds);
            Assert.IsFalse(profil.ShowRounds, "Die Kartoffel fragt nach Runden.");
            Assert.AreEqual(BuzzerControlsLayout.Minigame, profil.Layout);
        }

        /// <summary>V2: mit weniger als sechs Aussagen meldet die Pruefung „zu wenige"; eine leere Zeile zaehlt nicht.</summary>
        [TestMethod]
        public void SixStatementsAreTheMinimum()
        {
            var frage = new MinigameQuestion();
            MinigameProfiles.For(MinigameKind.HotPotato).ApplyDefaults(frage);

            for (var i = 0; i < 5; i++)
                frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = (i + 1) * 10, StepText = $"Aussage {i}", IsResult = i % 2 == 0 });

            frage.Steps.Add(new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 60, StepText = "  " });

            var meldung = MinigameChecks.Content(frage).SingleOrDefault(m => m.Code == MinigameChecks.TooFewOptions);
            Assert.IsNotNull(meldung, "Fuenf Aussagen gehen durch.");
            StringAssert.Contains(meldung.Message, "6 Aussagen");

            frage.Steps.Last().StepText = "Aussage 5";
            Assert.IsFalse(MinigameChecks.Content(frage).Any(m => m.Code == MinigameChecks.TooFewOptions), "Sechs Aussagen sind zu wenige.");
            Assert.IsTrue(MinigameChecks.ForPlayers(frage, 1).Any(m => m.Code == MinigameChecks.TooFewPlayers), "Allein laesst sie sich spielen.");
            Assert.IsFalse(MinigameChecks.ForPlayers(frage, 2).Any(), "Zu zweit laesst sie sich nicht spielen.");
        }
    }
}
