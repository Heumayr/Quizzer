using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Der Rundlauf durch die Übersetzer - <b>die tragende Zusicherung des Editor-Umbaus</b>.
    /// <para>
    /// <c>SaveWithStepsAsync</c> löscht jeden gespeicherten Schritt, dessen Id nicht in der
    /// übergebenen Liste steht. Ein Übersetzer, der beim Lesen etwas fallen lässt - einen eigenen
    /// Startschritt, ein <c>ResourceFileName</c>, einen zweiten Abschlussschritt -, löscht es
    /// beim nächsten Speichern <b>ohne jede Meldung</b>. Das ist der teuerste denkbare Fehler in
    /// einer Anwendung, in der abends vor einem Quiz gearbeitet wird.
    /// </para>
    /// <para>
    /// Die Regel dagegen: <b>jeder gelesene Schritt wird über seine Id durchgereicht und an Ort
    /// und Stelle verändert, nie neu gebaut.</b> Was die Maske nicht modelliert, fährt auf
    /// demselben Objekt unberührt mit.
    /// </para>
    /// </summary>
    [TestClass]
    public class SchrittbauRundlaufUnitTests
    {
        /// <summary>
        /// Eine Frage, die alles enthält, was ein Übersetzer verlieren könnte: einen selbst
        /// angelegten Startschritt, ein Medium, eine von <c>StepText</c> abweichende
        /// <c>Designation</c>, ein Häkchen und einen Abschluss.
        /// </summary>
        private static QuestionBase Vollbestueckt(QuestionType typ)
        {
            var frage = Factory.CreateNewQuestion(typ);

            frage.Id = Guid.NewGuid();
            frage.Designation = "Probe";
            frage.QuestionText = "Wie hoch ist der Großglockner?";

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frage.Id,
                SequenceNumber = 5,
                IsStart = true,
                Designation = "Eigener Startschritt",
                StepText = "Gleich geht es los.",
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frage.Id,
                SequenceNumber = 10,
                Designation = "Kurzform",
                StepText = "Der lange Text am Beamer",
                ResourceFileName = "bild.png",
                ResourceTyp = ResourceType.Image,
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frage.Id,
                SequenceNumber = 20,
                Designation = "Zweiter",
                StepText = "Noch ein Schritt",
                IsResult = true,
            });

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frage.Id,
                SequenceNumber = 30,
                IsFinish = true,
                Designation = "Auflösung",
                StepText = "3798 Meter",
            });

            return frage;
        }

        private static string Fingerabdruck(QuestionStepResource s)
            => string.Join("|",
                s.Id, s.Designation, s.StepText, s.ResourceFileName, s.ResourceTyp,
                s.IsStart, s.IsResult, s.IsFinish);

        /// <summary>
        /// <b>Lesen, nichts anfassen, zurückschreiben - und nichts hat sich geändert.</b> Für
        /// jeden Fragetyp einzeln, weil jeder Übersetzer anders aufteilt.
        /// <para>
        /// Die <c>SequenceNumber</c> ist ausgenommen: sie wird beim Ordnen ohnehin neu vergeben
        /// (<c>CalculateOrderdSteps</c> stempelt 0, 10, 20 …). Geprüft wird stattdessen, dass die
        /// <b>Reihenfolge</b> bleibt - das ist es, was sie trägt.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ReadingAndWritingBackChangesNothing()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var frage = Vollbestueckt(typ);

                var vorher = frage.Steps
                    .OrderBy(s => s.SequenceNumber)
                    .Select(Fingerabdruck)
                    .ToList();

                var composer = StepComposers.For(typ);

                composer.Schreib(frage, composer.Lies(frage));

                var nachher = frage.Steps
                    .OrderBy(s => s.SequenceNumber)
                    .Select(Fingerabdruck)
                    .ToList();

                CollectionAssert.AreEqual(vorher, nachher,
                    $"Der Rundlauf hat bei {typ} etwas veraendert oder verloren."
                    + Environment.NewLine + "vorher:  " + string.Join(Environment.NewLine + "         ", vorher)
                    + Environment.NewLine + "nachher: " + string.Join(Environment.NewLine + "         ", nachher));
            }
        }

        /// <summary>
        /// <b>Und die Schritte sind dieselben Objekte</b>, nicht gleich aussehende neue. Genau
        /// daran hängt es: <c>SaveWithStepsAsync</c> vergleicht Ids, und ein neu gebauter Schritt
        /// mit neuer Id löscht den alten und legt einen zweiten an.
        /// </summary>
        [TestMethod]
        public void TheStepsArePassedThroughNotRebuilt()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var frage = Vollbestueckt(typ);
                var vorher = frage.Steps.ToList();

                var composer = StepComposers.For(typ);

                composer.Schreib(frage, composer.Lies(frage));

                foreach (var alt in vorher)
                {
                    Assert.IsTrue(frage.Steps.Any(s => ReferenceEquals(s, alt)),
                        $"Bei {typ} ist der Schritt \"{alt.Designation}\" nicht mehr dasselbe "
                        + "Objekt - er wurde neu gebaut. Beim Speichern wuerde der alte "
                        + "geloescht und ein zweiter angelegt.");
                }
            }
        }

        /// <summary>
        /// Ein Startschritt, den der Spielleiter selbst angelegt hat, überlebt jeden Übersetzer.
        /// <b>In keiner Typmaske gibt es ein Feld dafür</b> - genau deshalb ist er der Kandidat,
        /// der als erstes verschwindet.
        /// </summary>
        [TestMethod]
        public void AnOwnStartStepSurvivesEveryComposer()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var frage = Vollbestueckt(typ);
                var composer = StepComposers.For(typ);

                composer.Schreib(frage, composer.Lies(frage));

                Assert.AreEqual(1, frage.Steps.Count(s => s.IsStart),
                    $"Bei {typ} ist der eigene Startschritt verlorengegangen oder verdoppelt "
                    + "worden.");
            }
        }

        /// <summary>
        /// <b>Der Startschritt ist jetzt ein eigenes Fach</b>, kein mitgeführter Schritt mehr.
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „generell wäre schön wenn man alles in einer maske
        /// steuert". Bis hierher war der Startschritt in der Typmaske unsichtbar und nicht
        /// anlegbar - bei der Schätzfrage überhaupt nicht erreichbar.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheStartStepLandsInItsOwnSlot()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var frage = Vollbestueckt(typ);
                var composer = StepComposers.For(typ);
                var bild = composer.Lies(frage);

                Assert.IsNotNull(bild.Start,
                    $"Bei {typ} gibt es kein Startfach - dann ist der Startschritt weiter "
                    + "unsichtbar.");

                Assert.AreEqual("Gleich geht es los.", bild.Start!.Text,
                    $"Bei {typ} steht der eigene Startschritt nicht im Startfach.");

                Assert.IsFalse(bild.Mitgefuehrt.Any(m => m.IsStart),
                    $"Bei {typ} faehrt der Startschritt zusaetzlich mit - dann wird er doppelt "
                    + "geschrieben.");
            }
        }

        /// <summary>
        /// Ein <b>leeres</b> Startfach schreibt nichts. Sonst hätte eine unberührte Maske plötzlich
        /// einen Schritt, und der Beamer zeigte einen leeren Bildschirm, den niemand angelegt hat.
        /// </summary>
        [TestMethod]
        public void AnEmptyStartSlotWritesNothing()
        {
            var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

            frage.Id = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.MultipleChoice);
            var bild = composer.Lies(frage);

            Assert.IsNotNull(bild.Start, "Auch eine neue Frage bekommt ein Startfach.");

            composer.Schreib(frage, bild);

            Assert.AreEqual(0, frage.Steps.Count,
                "Ein leeres Startfach wurde geschrieben.");

            bild.Start!.Text = "Gleich geht es los.";

            composer.Schreib(frage, bild);

            Assert.AreEqual(1, frage.Steps.Count);
            Assert.IsTrue(frage.Steps[0].IsStart,
                "Der aus dem Startfach geschriebene Schritt traegt die Kennung nicht.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung:</b> ein <i>zweiter</i> Startschritt bleibt mitgeführt. Nähme das
        /// Startfach nur den ersten und ließe den zweiten fallen, wäre das stiller Datenverlust -
        /// und die Zusicherung „genau ein Startschritt" bliebe trotzdem grün.
        /// </summary>
        [TestMethod]
        public void ASecondStartStepIsCarriedAlong()
        {
            var frage = Vollbestueckt(QuestionType.Default);

            frage.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = frage.Id,
                SequenceNumber = 7,
                IsStart = true,
                Designation = "Zweiter Startschritt",
                StepText = "Sollte es nicht geben - darf aber nicht verschwinden.",
            });

            var composer = StepComposers.For(QuestionType.Default);
            var bild = composer.Lies(frage);

            Assert.AreEqual(1, bild.Mitgefuehrt.Count(m => m.IsStart),
                "Der zweite Startschritt faehrt nicht mit - beim naechsten Speichern waere er weg.");

            composer.Schreib(frage, bild);

            Assert.AreEqual(2, frage.Steps.Count(x => x.IsStart),
                "Nach dem Rundlauf sind es nicht mehr zwei Startschritte.");
        }

        /// <summary>
        /// Ein Medium überlebt, obwohl keine Maske ein Feld dafür zeigt - es hängt am
        /// durchgereichten Schritt.
        /// </summary>
        [TestMethod]
        public void AttachedMediaSurviveEveryComposer()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var frage = Vollbestueckt(typ);
                var composer = StepComposers.For(typ);

                composer.Schreib(frage, composer.Lies(frage));

                Assert.AreEqual(1, frage.Steps.Count(s => s.ResourceFileName == "bild.png"),
                    $"Bei {typ} ist die Mediendatei am Schritt verlorengegangen.");
            }
        }

        /// <summary>
        /// Eine leere Zeile aus dem Gerüst wird nicht geschrieben. Sonst stünden bei Multiple
        /// Choice vier leere Antworten im Spiel, sobald jemand die Maske nur geöffnet hat.
        /// </summary>
        [TestMethod]
        public void EmptyScaffoldRowsAreNotWritten()
        {
            var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

            frage.Id = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.MultipleChoice);
            var bild = composer.Lies(frage);

            Assert.AreEqual(MultipleChoiceComposer.Vorgabezeilen, bild.Zeilen.Count,
                "Die Maske startet nicht mit dem Geruest aus leeren Antwortzeilen.");

            composer.Schreib(frage, bild);

            Assert.AreEqual(0, frage.Steps.Count,
                "Leere Geruestzeilen sind als Schritte geschrieben worden - im Spiel waeren das "
                + "leere Antwortmoeglichkeiten.");

            bild.Zeilen[0].Text = "Canberra";
            bild.Zeilen[0].IstRichtig = true;
            bild.Zeilen[1].Text = "Sydney";

            composer.Schreib(frage, bild);

            Assert.AreEqual(2, frage.Steps.Count,
                "Es werden nicht genau die gefuellten Zeilen geschrieben.");

            Assert.AreEqual(1, frage.BuzzerMaxAllowedKeySelect,
                "Die Zahl der waehlbaren Antworten folgt nicht der Zahl der Haken.");
        }

        /// <summary>
        /// Der Text der Zeile schreibt <b>beide</b> Textfelder. Am Beamer gewinnt
        /// <c>StepText</c>, am Telefon <c>Designation</c> - wer nur eines setzt, bekommt zwei
        /// verschiedene Antworten, ohne dass irgendetwas warnt.
        /// </summary>
        [TestMethod]
        public void TypingOnceFillsBothTextFields()
        {
            var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

            frage.Id = Guid.NewGuid();

            var composer = StepComposers.For(QuestionType.MultipleChoice);
            var bild = composer.Lies(frage);

            bild.Zeilen[0].Text = "Canberra";

            Assert.AreEqual("Canberra", bild.Zeilen[0].Schritt.StepText);
            Assert.AreEqual("Canberra", bild.Zeilen[0].Schritt.Designation,
                "Das Telefon zeigt die Designation auf der Taste - dort staende sonst nichts.");
        }

        /// <summary>
        /// Ein abgeleiteter Vorschlag überschreibt nie einen vorhandenen Text.
        /// <para>
        /// <b>Beim ersten Rundlauf gemessen und behoben:</b> das bloße Öffnen einer bestehenden
        /// Schätzfrage ersetzte die getippte Auflösung „3798 Meter" durch den abgeleiteten Text
        /// „0". Niemand hätte etwas angefasst, und beim nächsten Speichern wäre sie weg gewesen.
        /// </para>
        /// <para>
        /// Zugleich die Gegenrichtung: solange der Text dem Sollwert gefolgt <i>ist</i>, folgt er
        /// ihm weiter. Ohne diese zweite Zusicherung wäre der billige Fix „nie überschreiben"
        /// grün - und der Vorschlag nutzlos.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ASuggestionNeverOverwritesWhatSomeoneWrote()
        {
            var eigener = new StepZeile(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                StepText = "3798 Meter",
                Designation = "3798 Meter",
            });

            eigener.SchlageVor("0");

            Assert.AreEqual("3798 Meter", eigener.Text,
                "Ein Vorschlag hat einen vorhandenen Text ueberschrieben.");

            var leerer = new StepZeile(new QuestionStepResource { Id = Guid.NewGuid() });

            leerer.SchlageVor("etwa 3800 Meter");

            Assert.AreEqual("etwa 3800 Meter", leerer.Text,
                "In ein leeres Feld kommt kein Vorschlag - dann bringt er nichts.");

            leerer.SchlageVor("etwa 3798 Meter");

            Assert.AreEqual("etwa 3798 Meter", leerer.Text,
                "Der Vorschlag folgt dem Sollwert nicht mehr, obwohl niemand hineingetippt hat.");

            leerer.Text = "Der Großglockner, 3798 m";
            leerer.SchlageVor("etwas ganz anderes");

            Assert.AreEqual("Der Großglockner, 3798 m", leerer.Text,
                "Nach dem Hineintippen schlaegt es weiter vor.");
        }

        /// <summary>
        /// Die Taste, die in der Maske steht, ist die, die im Spiel gilt. <b>Aus derselben
        /// Vergabe</b>, nicht abgeschrieben.
        /// </summary>
        [TestMethod]
        public void TheKeyShownMatchesTheOneThePhoneGets()
        {
            var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

            frage.Id = Guid.NewGuid();
            frage.UseRandomSequenceOnNoneFinishSteps = false;

            var composer = StepComposers.For(QuestionType.MultipleChoice);
            var bild = composer.Lies(frage);

            bild.Zeilen[0].Text = "Sydney";
            bild.Zeilen[1].Text = "Canberra";
            bild.Zeilen[2].Text = "Melbourne";

            composer.Schreib(frage, bild);
            frage.CalculateOrderdSteps();

            var imSpiel = frage.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .ToDictionary(s => s.StepText, s => s.QuestionViewKey);

            foreach (var zeile in bild.Zeilen.Where(z => !z.IstLeer))
            {
                Assert.AreEqual(imSpiel[zeile.Text], zeile.Taste,
                    $"Fuer \"{zeile.Text}\" steht in der Maske die Taste \"{zeile.Taste}\", "
                    + $"im Spiel aber \"{imSpiel[zeile.Text]}\".");
            }
        }
    }
}
