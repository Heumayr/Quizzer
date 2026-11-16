using LocalBuzzer.Service.Base.States.Minispiele;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Views.GameViews.Sonderregeln;
using System.Windows;
using static Quizzer.Views.GameViews.Sub.PlayerResultContext;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Eine Regel ohne Vorphase (M35 C3b, Kopfgeld): sie steht beim Oeffnen fest, die Frage geht gleich auf die
    /// Telefone, Beamer und Spielleiter lesen ihren Hinweis, und die Karten rechnen mit ihr - ohne Minispiel-Rahmen.
    /// </summary>
    [TestClass]
    public class SonderregelOhneVorphaseUnitTests : SonderregelServerTestBasis
    {
        protected override int Port => 5421;

        /// <summary>Richtig zaehlt dreifach; merkt sich, was sie beim Oeffnen bekam.</summary>
        private sealed class SofortRegel()
            : SonderregelAblauf(new SpecialRuleProfile(CellSpecialRule.Stake, "Sofort", "Gilt ab dem Oeffnen."))
        {
            public override bool HatVorphase => false;

            public override string Hinweis => "Sofort-Hinweis";

            public VorphaseGrundlage? Grundlage { get; private set; }

            public override void Oeffne(VorphaseGrundlage grundlage) => Grundlage = grundlage;

            protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
            {
            }

            protected override string AbgabeText(Guid spieler) => "sofort";

            protected override Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang)
                => eingang.Bewertung == RatingKind.Right ? new(3 * eingang.Grundwert, "dreifach") : null;
        }

        [TestMethod]
        public async Task ARuleWithoutPrePhaseSendsTheQuestionStraightToThePhones()
        {
            var regel = new SofortRegel();
            var vm = await OeffneAsync(CellSpecialRule.Stake, spieler: 2, kontostaende: [70, 20], fabrik: _ => regel);
            var frageLayout = vm.Question!.BuzzerControlsLayout;
            var feld = world!.Coordinate.CurrentPoints;

            Assert.IsFalse(vm.VorphaseLaeuft, "Eine Regel ohne Vorphase spielt eine.");
            Assert.AreEqual(frageLayout, vm.CurrentBuzzerLayout, "Die Frage ging nicht gleich auf die Telefone.");
            Assert.AreNotEqual(MinigamePhase.Running, Zustand.Phase, "Am Server laeuft eine Vorphase.");
            Assert.IsTrue(await WarteAsync(() => serverVm!._server!.BuzzerController!.StateManager.CurrentLayout == frageLayout),
                "Am Server liegt nicht die Frage.");

            // Beim Oeffnen bekam die Regel Kontostaende und Namen.
            Assert.AreEqual(70, regel.Grundlage!.Kontostaende[world.Players[0].Id]);
            Assert.AreEqual(world.Players[1].CalculatedDisplayName, regel.Grundlage.Namen![world.Players[1].Id]);

            // Beamer und Spielleiter: kein Vorphasen-Bildschirm, aber der Hinweis ueber der Frage - mit Symbol.
            Assert.AreEqual(Visibility.Collapsed, vm.Sonderregel.BeamerVisibility);
            Assert.AreEqual(Visibility.Visible, vm.Sonderregel.HinweisVisibility, "Der Hinweis steht nicht ueber der Frage.");
            Assert.AreEqual($"{SpecialRuleProfiles.Symbol} Sofort-Hinweis", vm.Sonderregel.Hinweis);

            var anna = Karte(vm, 0);
            anna.Suggestion = ScoreSuggestion.Right;
            Assert.AreEqual(3 * feld, anna.CurrentScoreManipulation, "Die Regel rechnet ohne Vorphase nicht.");
            Assert.AreEqual("sofort", anna.SonderregelText);
        }
    }
}
