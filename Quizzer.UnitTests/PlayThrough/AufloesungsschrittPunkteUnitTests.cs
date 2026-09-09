using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.Base;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.UnitTests.PlayThrough
{
    /// <summary>
    /// <b>F16.</b> Erst der Auflösungsschritt gibt 0 Punkte - kein Hinweis davor.
    /// <para>
    /// Nutzerentscheidung vom 2026-09-09 im Wortlaut: „auch wenn alles erkennbar ist kann noch
    /// geraten werden ... es muss danach den aufloesungsschritt geben wo zB. der name der
    /// gesuchent figur angezeit wird ... ab dann 0 punkte".
    /// </para>
    /// <para>
    /// <b>Der Verlauf wird mitgeschrieben, nicht der Endzustand nachgesehen.</b> „Am Ende steht
    /// 0" wäre auch dann wahr, wenn schon der letzte Hinweis auf 0 gegangen wäre - und genau das
    /// ist der Fehler, den diese Entscheidung verhindert.
    /// </para>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class AufloesungsschrittPunkteUnitTests
    {
        private TestGameBuilder? world;

        [TestCleanup]
        public async Task Cleanup()
        {
            if (world != null)
                await world.DisposeAsync();

            UserPrompt.Reset();
        }

        /// <summary>Ein Messpunkt je Bildschirm: worauf wir stehen und was es noch gibt.</summary>
        private sealed record Messpunkt(bool IstAufloesung, bool IstHinweis, int Vorschlag);

        private async Task<List<Messpunkt>> VerlaufAsync(ScoreReductionMode modus)
        {
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();

            world = await TestGameBuilder.CreateAsync(
                QuestionType.Properties, normalStepCount: 3, playerCount: 2);

            var vm = new TestableCurrentQuestionViewModel { Coordinate = world.Coordinate };

            await vm.LoadForTestAsync();

            // ERST nach dem Laden: LoadForTestAsync holt die Frage frisch aus der Datenbank und
            // ueberschreibt jede Einstellung, die davor gesetzt wurde. Zuerst gemessen, dann
            // zugesichert - vorher gaben beide Kurven dieselben Zahlen, und das sah nach einem
            // Fehler in der Rechnung aus.
            var frage = vm.Coordinate!.QuestionBase!;

            frage.ScoreReductionMode = modus;
            frage.ScoreReductionFactor = 0.5;

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            var karte = vm.PlayersResultViewModel!.PlayerResultContextList[0];
            var verlauf = new List<Messpunkt>();

            for (var i = 0; i < 12; i++)
            {
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.None;
                karte.Suggestion = PlayerResultContext.ScoreSuggestion.Right;

                var schritt = vm.CurrentStep;

                verlauf.Add(new Messpunkt(
                    schritt?.IsFinish == true,
                    schritt != null && !schritt.IsStart && !schritt.IsFinish
                        && !schritt.IsQuestionOnly,
                    karte.CurrentScoreManipulation));

                if (vm.NextStep == null)
                    break;

                await vm.NextStepCommnad!.ExecuteAsync(null);
            }

            return verlauf;
        }

        [TestMethod]
        [DataRow(ScoreReductionMode.Linear)]
        [DataRow(ScoreReductionMode.Halving)]
        public async Task OnlyTheResolutionStepGivesNothing(ScoreReductionMode modus)
        {
            var verlauf = await VerlaufAsync(modus);

            var aufloesung = verlauf.Where(m => m.IstAufloesung).ToArray();
            var hinweise = verlauf.Where(m => m.IstHinweis).ToArray();

            Assert.AreEqual(1, aufloesung.Length,
                "Ohne genau einen Aufloesungsbildschirm misst dieser Test nichts: "
                + string.Join(", ", verlauf));

            Assert.AreEqual(0, aufloesung[0].Vorschlag,
                "Im Aufloesungsschritt darf es nichts mehr geben: "
                + string.Join(", ", verlauf.Select(m => m.Vorschlag)));

            foreach (var hinweis in hinweise)
            {
                Assert.IsTrue(hinweis.Vorschlag > 0,
                    "Solange nur Hinweise stehen, kann noch geraten werden - es muss etwas zu "
                    + "holen geben: " + string.Join(", ", verlauf.Select(m => m.Vorschlag)));
            }
        }

        /// <summary>
        /// Die Gegenrichtung: der Verlauf über die Hinweise fällt wirklich, statt gleich zu
        /// bleiben.
        /// <para>
        /// <b>Ohne diese Zusicherung wäre die obige auch dann grün, wenn gar kein Abzug
        /// stattfände</b> - jeder Hinweis gäbe die vollen Punkte, und nur die Auflösung 0.
        /// </para>
        /// </summary>
        [TestMethod]
        [DataRow(ScoreReductionMode.Linear)]
        [DataRow(ScoreReductionMode.Halving)]
        public async Task EveryHintCostsSomething(ScoreReductionMode modus)
        {
            var hinweise = (await VerlaufAsync(modus))
                .Where(m => m.IstHinweis)
                .Select(m => m.Vorschlag)
                .ToArray();

            Assert.IsTrue(hinweise.Length >= 3,
                "Zu wenige Hinweisbildschirme, um einen Verlauf zu messen: "
                + string.Join(", ", hinweise));

            for (var i = 1; i < hinweise.Length; i++)
            {
                Assert.IsTrue(hinweise[i] < hinweise[i - 1],
                    $"Bildschirm {i + 1} kostet nichts: " + string.Join(", ", hinweise));
            }
        }
    }
}
