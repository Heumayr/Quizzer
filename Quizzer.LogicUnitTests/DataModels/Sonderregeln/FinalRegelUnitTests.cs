using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.LogicUnitTests.DataModels.Sonderregeln
{
    /// <summary>
    /// Das Finale als reine Regel (M37 E4, E01): mit und ohne Teams gespielt, mit Vorphase; richtig gewinnt den eigenen Einsatz,
    /// falsch verliert ihn, ohne Einsatz zählt 0, gekappt nach E2. Aufgelöst wird aufsteigend nach Stand. Höchstens eins je Spiel:
    /// das Verteilen setzt es nie, und ein zweites fällt beim Spielstart auf.
    /// </summary>
    [TestClass]
    public class FinalRegelUnitTests
    {
        private static GameGridCoordinate Zelle(CellSpecialRule regel = CellSpecialRule.None, int x = 0, int y = 0)
        {
            var frage = new DefaultQuestion { Id = Guid.NewGuid() };
            return new() { Id = Guid.NewGuid(), X = x, Y = y, QuestionBase = frage, QuestionBaseId = frage.Id, SpecialRule = regel };
        }

        private static SonderregelEingang Eingang(RatingKind bewertung, int kontostand = 1000, bool unterNull = true, int grundwert = 0)
            => new(bewertung, grundwert, 300, new Kappung(kontostand, unterNull));

        [TestMethod]
        public void TheFinalIsPlayedWithAndWithoutTeamsAndHasAStakePhase()
        {
            var zelle = Zelle(CellSpecialRule.Final);

            Assert.IsTrue(SpecialRuleProfiles.TryFor(CellSpecialRule.Final, out var profil));
            Assert.AreEqual("Finale", profil.DisplayName);
            Assert.IsTrue(profil.HasPrePhase, "Das Finale setzt vorher nichts.");
            Assert.IsTrue(profil.EinmalJeSpiel);
            Assert.IsFalse(profil.IstTeamregel);
            Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: false));
            Assert.IsTrue(SpecialRuleProfiles.Applies(zelle, zelle.QuestionBase, teamspiel: true), "Im Teamspiel gibt es kein Finale.");
        }

        [TestMethod]
        public void RightWinsAndWrongLosesTheOwnStake()
        {
            Assert.AreEqual(200, FinalRegel.Werte(200, Eingang(RatingKind.Right))?.Punkte);
            Assert.AreEqual(-200, FinalRegel.Werte(200, Eingang(RatingKind.Wrong))?.Punkte);
            Assert.AreEqual("Einsatz 200", FinalRegel.Werte(200, Eingang(RatingKind.Right))?.Grund);
            Assert.IsNull(FinalRegel.Werte(200, Eingang(RatingKind.None)), "Eine unbewertete Karte bewegt Punkte.");

            // Ein Teilwert wie beim Einsatz: 150 von 300 Feldpunkten, auf 200 übertragen.
            Assert.AreEqual(100, FinalRegel.Werte(200, Eingang(RatingKind.Partial, grundwert: 150))?.Punkte);
        }

        /// <summary>Anders als beim Einsatz spielt, wer nichts gesetzt hat, nicht um die Feldpunkte - er setzt 0.</summary>
        [TestMethod]
        public void WithoutAStakeNothingMoves()
        {
            Assert.AreEqual(0, FinalRegel.Werte(null, Eingang(RatingKind.Right))?.Punkte, "Ohne Einsatz zählen die Feldpunkte.");
            Assert.AreEqual(0, FinalRegel.Werte(null, Eingang(RatingKind.Wrong))?.Punkte);
        }

        /// <summary>E2: ohne Minus im Spiel verliert man höchstens seinen Kontostand.</summary>
        [TestMethod]
        public void ALossIsCappedAtTheOwnBalance()
        {
            var vorschlag = FinalRegel.Werte(200, Eingang(RatingKind.Wrong, kontostand: 50, unterNull: false));

            Assert.AreEqual(-50, vorschlag?.Punkte);
            StringAssert.Contains(vorschlag!.Grund, "Einsatz 200");
        }

        [TestMethod]
        public void TheRevealGoesUpFromTheLowestBalanceTiesByNameThenId()
        {
            var a = new FinalPlatz(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Bert", 300);
            var b = new FinalPlatz(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Bert", 300);
            var c = new FinalPlatz(Guid.NewGuid(), "Anna", 300);
            var d = new FinalPlatz(Guid.NewGuid(), "Zoe", -50);
            var e = new FinalPlatz(Guid.NewGuid(), "Carl", 900);

            var folge = FinalRegel.Reihenfolge([e, a, c, b, d]);

            CollectionAssert.AreEqual(new[] { d, c, b, a, e }, folge.ToArray(), string.Join(", ", folge.Select(p => p.Name)));
        }

        [TestMethod]
        public void TheAnswerTextQuotesAndNamesAMissingAnswer()
        {
            Assert.AreEqual("„Ottawa“", FinalRegel.AntwortText("  Ottawa "));
            Assert.AreEqual("keine Antwort", FinalRegel.AntwortText(null));
            Assert.AreEqual("keine Antwort", FinalRegel.AntwortText("   "));
        }

        /// <summary>Gewürfelt läge das Finale irgendwo und womöglich doppelt - das Verteilen setzt es nie, auch wenn man es ihm gibt.</summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DistributingNeverPlacesTheFinal(bool teamspiel)
        {
            var zellen = Enumerable.Range(0, 30).Select(_ => Zelle()).ToList();
            var auftrag = new Verteilauftrag(30, false, [CellSpecialRule.Final, CellSpecialRule.Stake], new Random(5).Next) { Teamspiel = teamspiel };

            var belegt = Sonderregelraster.Verteile(zellen, auftrag);

            Assert.AreEqual(30, belegt.Count);
            Assert.IsTrue(belegt.All(z => z.SpecialRule == CellSpecialRule.Stake), "Das Verteilen hat ein Finale gesetzt.");
            Assert.AreEqual(0, Sonderregelraster.Verteile([Zelle()], auftrag with { Regeln = [CellSpecialRule.Final] }).Count);
        }

        [TestMethod]
        public void ASecondFinalIsAWarningAtTheGameStart()
        {
            var eins = Zelle(CellSpecialRule.Final, x: 4, y: 3);
            var zellen = new List<GameGridCoordinate> { eins, Zelle(CellSpecialRule.Stake), Zelle(CellSpecialRule.Stake), Zelle() };

            Assert.AreEqual(0, Sonderregelraster.Spielbefunde(zellen).Count, "Ein Finale und zwei Einsätze sind kein Befund.");

            zellen.Add(Zelle(CellSpecialRule.Final, x: 0, y: 1));
            var befund = Sonderregelraster.Spielbefunde(zellen).Single();

            Assert.AreEqual(Sonderregelraster.OnlyOncePerGameCode, befund.Code);
            Assert.AreEqual(ValidationSeverity.Warning, befund.Severity, "Ein zweites Finale hält den Start an.");
            StringAssert.Contains(befund.Message, "„Finale“ liegt 2-mal im Raster (Zelle 1/2, Zelle 5/4)");
        }

        [TestMethod]
        public void TheFinalIsFreeOnlyWhereNoOtherCellHasIt()
        {
            var finale = Zelle(CellSpecialRule.Final);
            var andere = Zelle();
            var raster = new[] { finale, andere };
            var klon = new GameGridCoordinate { Id = finale.Id, SpecialRule = CellSpecialRule.Final };

            Assert.IsTrue(Sonderregelraster.FreiFuer(CellSpecialRule.Final, finale, raster), "Die Zelle mit dem Finale darf es nicht behalten.");
            Assert.IsTrue(Sonderregelraster.FreiFuer(CellSpecialRule.Final, klon, raster), "Ein Klon derselben Zelle gilt als andere Zelle.");
            Assert.IsFalse(Sonderregelraster.FreiFuer(CellSpecialRule.Final, andere, raster), "Ein zweites Finale ist frei.");
            Assert.IsTrue(Sonderregelraster.FreiFuer(CellSpecialRule.Stake, andere, [Zelle(CellSpecialRule.Stake), andere]),
                "Ein Einsatz gilt als einmal je Spiel.");

            // Zwei ungespeicherte Zellen sind zwei Zellen, auch mit derselben leeren Kennung.
            var neu = new GameGridCoordinate { SpecialRule = CellSpecialRule.Final };
            Assert.IsFalse(Sonderregelraster.FreiFuer(CellSpecialRule.Final, new GameGridCoordinate(), [neu]));
        }
    }
}
