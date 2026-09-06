using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// Multiple Choice baut keine Punkte ab, wenn eine Antwortmöglichkeit aufgedeckt wird.
    /// <para>
    /// <b>Nutzervorgabe vom 2026-09-06 abends:</b> „Mc sollte keine Punkte mit jeder Antwort
    /// Möglichkeiten abbauen". Der Abzug je Schritt ist für die Eigenschaftsfrage gemeint - dort
    /// ist jeder Schritt ein Hinweis und kostet. Bei Multiple Choice sind die Schritte die
    /// <b>Antwortmöglichkeiten</b>; sie aufzudecken verrät nichts.
    /// </para>
    /// <para>
    /// <b>Nachgemessen am 2026-09-06, bevor etwas gebaut wurde:</b> das Profil setzt
    /// <c>UseProportionalScoreReductionOnStep = false</c> für Multiple Choice, und alle sechs
    /// MC-Fragen der Spieldatenbank tragen die Spalte auf 0 - die Vorgabe war also bereits
    /// erfüllt. <b>Gehalten hat sie nur nichts.</b> Die beiden vorhandenen Zusicherungen prüfen
    /// ausschließlich die Gegenrichtung (die Eigenschaftsfrage <i>hat</i> den Abzug); dass ein
    /// anderer Typ ihn nicht hat, stand nirgends.
    /// </para>
    /// </summary>
    [TestClass]
    public class MultipleChoicePunkteUnitTests
    {
        private TestGameBuilder world = null!;

        [TestCleanup]
        public async Task TearDown()
        {
            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
        }

        /// <summary>
        /// Spielt die Frage Schritt für Schritt durch und schreibt mit, welchen Punktevorschlag
        /// die Bewertung „richtig" bei jedem Schritt macht.
        /// <para>
        /// <b>Der Verlauf wird mitgeschrieben, nicht der Endzustand nachgesehen.</b> „Am Ende
        /// stimmen die Punkte" wäre auch dann wahr, wenn sie zwischendurch eingebrochen wären.
        /// </para>
        /// </summary>
        /// <summary>Was bei einem Schritt gemessen wurde: wie viele Schritte davor lagen und
        /// welchen Punktevorschlag „richtig" dort macht.</summary>
        private sealed record Messpunkt(int Aufgedeckt, int Vorschlag);

        private async Task<List<Messpunkt>> VorschlaegeJeSchrittAsync(QuestionType typ)
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(typ, normalStepCount: 4, playerCount: 2);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var karte = vm.PlayersResultViewModel!.PlayerResultContextList[0];
            var verlauf = new List<Messpunkt>();

            for (var i = 0; i < 12; i++)
            {
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.None;
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.Right;

                verlauf.Add(new Messpunkt(karte.PreviousStepsCount, karte.CurrentScoreManipulation));

                if (vm.NextStep == null)
                    break;

                await vm.NextStepCommnad!.ExecuteAsync(null);
            }

            return verlauf;
        }

        /// <summary>
        /// Die Punkte bleiben über alle vier Antwortmöglichkeiten gleich.
        /// </summary>
        [TestMethod]
        public async Task RevealingAnOptionCostsNoPoints()
        {
            var verlauf = await VorschlaegeJeSchrittAsync(QuestionType.MultipleChoice);

            Assert.IsTrue(verlauf[^1].Aufgedeckt >= 4,
                "Die Probe ist nicht bis hinter alle vier Antwortmoeglichkeiten gekommen - dann "
                + "sagt sie nichts ueber das Aufdecken. Verlauf: " + Zeig(verlauf));

            Assert.IsTrue(verlauf[0].Vorschlag > 0,
                "Schon der erste Schritt gibt keine Punkte - dann misst diese Probe nichts. "
                + "Verlauf: " + Zeig(verlauf));

            Assert.IsTrue(verlauf.TrueForAll(p => p.Vorschlag == verlauf[0].Vorschlag),
                "Die Punkte sinken, während die Antwortmöglichkeiten aufgedeckt werden. "
                + "Verlauf: " + Zeig(verlauf));
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn die Probe eine
        /// sinkende Punktzahl gar nicht sehen könnte - etwa weil der Vorschlag nie neu gerechnet
        /// wird. Bei der Eigenschaftsfrage <i>muss</i> derselbe Weg sinkende Werte liefern.
        /// </summary>
        [TestMethod]
        public async Task WithHintsTheSameProbeSeesTheScoreShrink()
        {
            var verlauf = await VorschlaegeJeSchrittAsync(QuestionType.Properties);

            Assert.IsTrue(verlauf[^1].Vorschlag < verlauf[0].Vorschlag,
                "Die Probe sieht keinen Abbau, obwohl die Eigenschaftsfrage ihn haben muss - "
                + "dann sagt die Zusicherung zu Multiple Choice nichts. Verlauf: "
                + Zeig(verlauf));
        }

        private static string Zeig(List<Messpunkt> verlauf)
            => string.Join(", ", verlauf.Select(m => $"{m.Aufgedeckt}:{m.Vorschlag}"));

        /// <summary>
        /// Und der Typ selbst legt es fest: der Abzug ist bei Multiple Choice keine Einstellung,
        /// die eine einzelne Frage umdrehen könnte.
        /// </summary>
        [TestMethod]
        public void TheTypeItselfRulesOutTheDeduction()
        {
            Assert.IsFalse(
                QuestionTypeProfiles.For(QuestionType.MultipleChoice).UseProportionalScoreReductionOnStep,
                "Das Profil erlaubt den Punkteabzug wieder.");

            Assert.IsTrue(
                QuestionTypeProfiles.For(QuestionType.Properties).UseProportionalScoreReductionOnStep,
                "Die Eigenschaftsfrage hat ihren Abzug verloren - dann prueft die Zusicherung "
                + "darueber nur, dass ueberall nichts abgezogen wird.");
        }
    }
}
