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
        /// <param name="Sichtbar">
        /// Wie viele Inhaltsschritte <b>auf dem Bildschirm stehen</b> - gezählt an den
        /// <c>DisplaySteps</c>, die der Beamer bindet, nicht an der Größe, mit der die Punkte
        /// gerechnet werden.
        /// <para>
        /// <b>Das ist der springende Punkt dieser Probe.</b> Die erste Fassung nahm dafür
        /// <c>GezeigteSchritte</c> - also genau den Wert, den auch die Punkterechnung benutzt.
        /// Sie verglich damit die Formel gegen sich selbst und blieb grün, als der Fehler
        /// absichtlich wieder eingebaut wurde. Gemessen wird jetzt gegen eine <b>unabhängige</b>
        /// Größe: was der Spieler sieht.
        /// </para>
        /// </param>
        private sealed record Messpunkt(int Sichtbar, int Regular, int Punkte, int Vorschlag);

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

                verlauf.Add(new Messpunkt(
                    vm.CurrentStepContext?.DisplaySteps?.Count(d => d.IsVisibleSlot) ?? -1,
                    karte.RegularStepCount,
                    karte.Coordinate?.CurrentPoints ?? 0,
                    karte.CurrentScoreManipulation));

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

            Assert.IsTrue(verlauf[^1].Sichtbar >= 4,
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
            => string.Join(", ", verlauf.Select(m => $"{m.Sichtbar}/{m.Regular}:{m.Vorschlag}"));

        /// <summary>
        /// <b>Was der Editor neben einen Hinweis schreibt, gibt das Spiel auch.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07 mit einer Sonde, bevor etwas geändert wurde:</b> auf dem
        /// Bildschirm mit allen drei Hinweisen gab das Spiel 68 von 200 Punkten, während der
        /// Frageneditor neben denselben dritten Hinweis „danach noch 2" schreibt. Der Abzug hing
        /// genau einen Schritt hinterher - der gerade gezeigte Hinweis wurde nicht mitgezählt,
        /// obwohl er auf dem Beamer steht.
        /// </para>
        /// <para>
        /// <c>Punkteabzug</c> nennt genau diesen Gleichlauf als seinen Daseinsgrund: <i>„Was der
        /// Editor verspricht, muss das Spiel auch geben."</i>
        /// </para>
        /// </summary>
        [TestMethod]
        public async Task TheGameGivesWhatTheEditorPromises()
        {
            var verlauf = await VorschlaegeJeSchrittAsync(QuestionType.Properties);

            // Hinweiszahl und Punkte werden GEMESSEN, nicht angenommen: die erste Fassung dieser
            // Probe hatte beides geraten und meldete daraufhin einen Fehler, den es nicht gab.
            var abweichungen = verlauf
                .Where(m => m.Sichtbar > 0)
                .Select(m => new
                {
                    m.Sichtbar,
                    Spiel = m.Vorschlag,
                    Editor = Punkteabzug.Verbleibend(m.Punkte, m.Regular, m.Sichtbar),
                })
                .Where(x => x.Spiel != x.Editor)
                .ToList();

            Assert.IsTrue(verlauf.Any(m => m.Sichtbar == verlauf[0].Regular && m.Regular > 0),
                "Die Probe ist nie bis hinter den letzten Hinweis gekommen - dann sagt sie "
                + "nichts. Verlauf: " + Zeig(verlauf));

            Assert.AreEqual(0, abweichungen.Count,
                "Das Spiel gibt andere Punkte als der Editor ansagt: "
                + string.Join(", ", abweichungen.Select(x =>
                    $"bei {x.Sichtbar} sichtbaren Hinweisen Spiel={x.Spiel} Editor={x.Editor}")));
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn <i>gar keine</i>
        /// Punkte mehr abgezogen würden und der Editor dasselbe behauptete - der Vergleich prüft
        /// ja nur Gleichlauf. Hier steht, dass der Abzug überhaupt greift.
        /// </summary>
        [TestMethod]
        public async Task TheFirstHintAlreadyCosts()
        {
            var verlauf = await VorschlaegeJeSchrittAsync(QuestionType.Properties);

            var voll = world.Coordinate.CurrentPoints;
            var beimErsten = verlauf.First(m => m.Sichtbar == 1).Vorschlag;

            Assert.IsTrue(beimErsten < voll,
                $"Der erste Hinweis kostet nichts ({beimErsten} von {voll}) - dann ist er "
                + "geschenkt, und der Punkteabzug beginnt erst beim zweiten.");
        }

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
