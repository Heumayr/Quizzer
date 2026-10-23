using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.DataModels.Transfer.Ki;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// M31: aus jedem KI-Entwurf wird eine Frage, die der Validator ohne Fehler annimmt - und aus
    /// einem unbrauchbaren keine, mit Grund.
    /// </summary>
    [TestClass]
    public class KiEntwurfUebersetzerUnitTests
    {
        private static readonly Guid Kategorie = Guid.NewGuid();
        private static readonly DateTime Heute = new(2026, 10, 23);

        private static string? Bild(int index) => index == 0 ? "ki-bild.png" : null;

        private static KiZeile Z(string text, bool richtig = false, string? partner = null) => new() { Text = text, Richtig = richtig, Partner = partner };

        private static KiFrageEntwurf Entwurf(QuestionType typ, params KiZeile[] zeilen) => new()
        {
            Typ = typ,
            Fragetext = "Probe: " + typ,
            Bezeichnung = "Probe " + typ,
            Kurz = "PRB",
            Punkte = 200,
            Schwierigkeit = 2,
            Zeilen = zeilen.ToList(),
            Aufloesung = "Die Lösung",
            Quellen = ["https://de.wikipedia.org/wiki/Probe"],
        };

        public static IEnumerable<object[]> GuteEntwuerfe()
        {
            yield return [Entwurf(QuestionType.Default, Z("Hinweis 1"), Z("Hinweis 2"))];
            yield return [Entwurf(QuestionType.Properties, Z("Eigenschaft 1"), Z("Eigenschaft 2"), Z("Eigenschaft 3"))];
            yield return [Entwurf(QuestionType.MultipleChoice, Z("A", true), Z("B"), Z("C"), Z("D"))];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Appreciate, Fragetext = "Wie hoch?", Sollwert = 3798, Einheit = "Meter", Punkte = 100 }];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Appreciate, Fragetext = "Wann?", SollDatum = "1989-11-09", Aufloesung = "9. November 1989" }];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Reveal, Fragetext = "Was ist das?", BildIndex = 0, Aufloesung = "Ein Bild" }];
            yield return [Entwurf(QuestionType.Ordering, Z("Erst"), Z("Dann"), Z("Zuletzt"))];
            yield return [Entwurf(QuestionType.Matching, Z("Österreich", partner: "Wien"), Z("Schweiz", partner: "Bern"), Z("Deutschland", partner: "Berlin"))];
            yield return [Entwurf(QuestionType.TrueFalse, Z("Wale sind Säugetiere.", true), Z("Spinnen sind Insekten."))];
        }

        [TestMethod]
        [DynamicData(nameof(GuteEntwuerfe))]
        public void EveryTypeBecomesAQuestionTheValidatorAccepts(KiFrageEntwurf entwurf)
        {
            var ergebnis = KiEntwurfUebersetzer.Baue(entwurf, Kategorie, Bild, Heute);

            Assert.IsNotNull(ergebnis.Frage, string.Join("; ", ergebnis.Hinweise));
            Assert.AreEqual(entwurf.Typ, ergebnis.Frage!.Typ);
            Assert.AreEqual(Kategorie, ergebnis.Frage.CategoryId);
            Assert.AreNotEqual(Guid.Empty, ergebnis.Frage.Id);

            var fehler = QuestionValidator.Validate(ergebnis.Frage).Where(i => i.IsError).ToList();
            Assert.AreEqual(0, fehler.Count, string.Join("; ", fehler.Select(f => f.Message)));

            StringAssert.StartsWith(ergebnis.Frage.Notes, "KI-Entwurf vom 23.10.2026");

            // Der Editor liest die Frage über die Übersetzer - das darf nicht werfen.
            StepComposers.For(entwurf.Typ).Lies(ergebnis.Frage);
        }

        [TestMethod]
        public void MultipleChoiceCountsTheCorrectAnswers()
        {
            var frage = (MultipleChoiceQuestion)KiEntwurfUebersetzer.Baue(
                Entwurf(QuestionType.MultipleChoice, Z("A", true), Z("B", true), Z("C")), Kategorie, Bild, Heute).Frage!;

            Assert.AreEqual(2, frage.BuzzerMaxAllowedKeySelect);
            Assert.IsFalse(frage.Steps.Single(s => s.IsFinish).IsResult, "Der Abschluss zaehlte sonst als dritte Loesung.");
        }

        [TestMethod]
        public void MatchingPutsTheCounterpartRightAndTheTermLeft()
        {
            var frage = KiEntwurfUebersetzer.Baue(
                Entwurf(QuestionType.Matching, Z("Österreich", partner: "Wien"), Z("Schweiz", partner: "Bern"), Z("Italien", partner: "Rom")), Kategorie, Bild, Heute).Frage!;

            var erster = frage.Steps.OrderBy(s => s.SequenceNumber).First();
            Assert.AreEqual("Wien", erster.StepText);
            Assert.AreEqual("Österreich", erster.PartnerText);
            Assert.AreEqual(1, erster.SolutionNumber);
        }

        [TestMethod]
        public void AnEstimateTakesItsKindFromTheUnit()
        {
            var frage = (AppreciateQestion)KiEntwurfUebersetzer.Baue(
                new KiFrageEntwurf { Typ = QuestionType.Appreciate, Fragetext = "Wie schwer?", Sollwert = 6, Einheit = "tonne" }, Kategorie, Bild, Heute).Frage!;

            Assert.AreEqual(AppreciateValueKind.Mass, frage.ValueKind);
            Assert.AreEqual(6, frage.ExpectedValue);
            Assert.IsFalse(string.IsNullOrWhiteSpace(frage.Steps.Single(s => s.IsFinish).StepText), "Ohne Aufloesung der KI soll der Sollwert dort stehen.");
        }

        [TestMethod]
        public void AnImageGoesToTheFirstHintNotTheSolution()
        {
            var entwurf = Entwurf(QuestionType.Default, Z("Hinweis 1"));
            entwurf.BildIndex = 0;

            var frage = KiEntwurfUebersetzer.Baue(entwurf, Kategorie, Bild, Heute).Frage!;
            var erster = frage.Steps.OrderBy(s => s.SequenceNumber).First();

            Assert.AreEqual("ki-bild.png", erster.ResourceFileName);
            Assert.AreEqual(ResourceType.Image, erster.ResourceTyp);
            Assert.IsNull(frage.Steps.Single(s => s.IsFinish).ResourceFileName is { Length: > 0 } ? "falsch" : null);
        }

        public static IEnumerable<object[]> SchlechteEntwuerfe()
        {
            yield return [Entwurf(QuestionType.MultipleChoice, Z("A"), Z("B")), "keine Antwort"];
            yield return [Entwurf(QuestionType.MultipleChoice, Z("A", true), Z("B", true)), "alle Antworten"];
            yield return [Entwurf(QuestionType.MultipleChoice, Z("A", true)), "mindestens zwei"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Appreciate, Fragetext = "?" }, "ohne Sollwert"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Appreciate, Fragetext = "?", SollDatum = "9.11.1989" }, "kein Datum"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Reveal, Fragetext = "?", Aufloesung = "x" }, "braucht ein Bild"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Reveal, Fragetext = "?", Aufloesung = "x", BildIndex = 3 }, "braucht ein Bild"];
            yield return [Entwurf(QuestionType.Ordering, Z("A"), Z("B")), "mindestens drei"];
            yield return [Entwurf(QuestionType.Matching, Z("A", partner: "1"), Z("B"), Z("C")), "drei Paare"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Default, Fragetext = "  " }, "keinen Fragetext"];
            yield return [new KiFrageEntwurf { Typ = QuestionType.Default, Fragetext = "?" }, "keine Auflösung"];
        }

        [TestMethod]
        [DynamicData(nameof(SchlechteEntwuerfe))]
        public void AnUnusableDraftSaysWhy(KiFrageEntwurf entwurf, string grund)
        {
            var ergebnis = KiEntwurfUebersetzer.Baue(entwurf, Kategorie, Bild, Heute);

            Assert.IsNull(ergebnis.Frage);
            Assert.IsTrue(ergebnis.Hinweise.Any(h => h.Contains(grund, StringComparison.OrdinalIgnoreCase)), string.Join("; ", ergebnis.Hinweise));
        }

        /// <summary>Was die KI liefert, ist nicht vertrauenswürdig: Längen und Zahlen werden begrenzt.</summary>
        [TestMethod]
        public void LengthsAndNumbersAreBounded()
        {
            var entwurf = Entwurf(QuestionType.Default, Enumerable.Range(1, 20).Select(i => Z(new string('x', 2000))).ToArray());
            entwurf.Fragetext = new string('?', 5000);
            entwurf.Punkte = 1_000_000;
            entwurf.Schwierigkeit = 99;

            var ergebnis = KiEntwurfUebersetzer.Baue(entwurf, Kategorie, Bild, Heute);
            var frage = ergebnis.Frage!;

            Assert.AreEqual(KiEntwurfUebersetzer.MaxFragetext, frage.QuestionText.Length);
            Assert.AreEqual(5000, frage.Points);
            Assert.AreEqual(Difficulty.Level1, frage.Difficulty);
            Assert.AreEqual(KiEntwurfUebersetzer.MaxZeilen + 1, frage.Steps.Count, "Zeilen plus Abschluss");
            Assert.IsTrue(frage.Steps.All(s => s.StepText.Length <= KiEntwurfUebersetzer.MaxZeile));
            Assert.IsTrue(ergebnis.Hinweise.Any(h => h.Contains("ersten")));
        }
    }
}
