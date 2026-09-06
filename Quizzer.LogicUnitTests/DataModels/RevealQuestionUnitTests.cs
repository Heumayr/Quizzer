using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Die Aufdeckfrage: Rechnung, Klon und der Weg durch das Bündel.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06:</b> „man lädt ein bild rein im frageeditor ... dann kann
    /// man nach und nach bildausschnitte verdecken ... im spiel wird dann nach und nach das bild
    /// aufgedeckt bis es erraten wird" und „2 modus ... es wird mittels filter extrem unscharf
    /// gemacht ... jeder step macht es schärfer".
    /// </para>
    /// </summary>
    [TestClass]
    public class RevealQuestionUnitTests
    {
        private static readonly List<RevealArea> DreiFlaechen =
        [
            new(0.0, 0.0, 0.5, 0.5),
            new(0.5, 0.0, 0.5, 0.5),
            new(0.0, 0.5, 1.0, 0.5),
        ];

        /// <summary>Je Schritt fällt genau eine Fläche weg - und am Ende ist das Bild frei.</summary>
        [TestMethod]
        public void OneAreaFallsPerStep()
        {
            var verlauf = Enumerable.Range(0, 5)
                .Select(i => RevealAreas.NochVerdeckt(DreiFlaechen, i).Count())
                .ToArray();

            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0, 0 }, verlauf,
                "Die Flaechen fallen nicht eine nach der anderen: " + string.Join(", ", verlauf));

            Assert.AreSame(DreiFlaechen[2], RevealAreas.NochVerdeckt(DreiFlaechen, 2).Single(),
                "Es faellt nicht die ERSTE Flaeche zuerst - dann deckt das Spiel in einer "
                + "anderen Reihenfolge auf, als der Spielleiter gezeichnet hat.");
        }

        /// <summary>
        /// Die Unschärfe läuft auf null - der letzte Schritt zeigt das Bild scharf.
        /// <para>
        /// <b>Die zweite Zusicherung ist die wichtigere:</b> ohne Inhaltsschritte gibt es nichts
        /// zu schärfen. Bliebe die Unschärfe dann stehen, wäre das eine Frage, die niemand
        /// beantworten kann.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheBlurEndsAtZero()
        {
            var verlauf = Enumerable.Range(0, 5)
                .Select(i => RevealAreas.Unschaerfe(40, 4, i))
                .ToArray();

            CollectionAssert.AreEqual(new[] { 40.0, 30.0, 20.0, 10.0, 0.0 }, verlauf,
                "Die Unschaerfe laeuft nicht gleichmaessig auf null: "
                + string.Join(", ", verlauf));

            Assert.AreEqual(0, RevealAreas.Unschaerfe(40, 0, 0),
                "Ohne Inhaltsschritte bleibt das Bild unscharf - dann kann die Frage niemand "
                + "beantworten.");

            Assert.AreEqual(0, RevealAreas.Unschaerfe(40, 3, 99),
                "Ueber das Ende hinaus wird die Unschaerfe negativ oder bleibt stehen.");
        }

        /// <summary>Eine unlesbare Flächenangabe ergibt keine Ausnahme, sondern nichts.</summary>
        [TestMethod]
        public void BrokenAreasAreIgnoredInsteadOfThrowing()
        {
            Assert.AreEqual(0, RevealAreas.Parse("das ist kein JSON").Count);
            Assert.AreEqual(0, RevealAreas.Parse(null).Count);
            Assert.AreEqual(3, RevealAreas.Parse(RevealAreas.ToJson(DreiFlaechen)).Count);
        }

        /// <summary>
        /// <b>Der Klon nimmt die typeigenen Felder mit.</b> Der Schreibweg schreibt einen Klon,
        /// nie die übergebene Frage - was er nicht mitnimmt, wird lautlos nie gespeichert.
        /// </summary>
        [TestMethod]
        public void TheCloneCarriesTheRevealFields()
        {
            var frage = new RevealQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Wer ist das?",
                Mode = RevealMode.Blur,
                ImageFileName = "bild.png",
                AreasJson = RevealAreas.ToJson(DreiFlaechen),
                BlurStart = 55,
            };

            var klon = (RevealQuestion)frage.CloneWithoutReferences();

            Assert.AreEqual(RevealMode.Blur, klon.Mode, "Die Betriebsart ging verloren.");
            Assert.AreEqual("bild.png", klon.ImageFileName, "Das Bild ging verloren.");
            Assert.AreEqual(3, RevealAreas.Parse(klon.AreasJson).Count, "Die Flaechen gingen verloren.");
            Assert.AreEqual(55, klon.BlurStart, "Die Unschaerfe ging verloren.");
        }

        /// <summary>
        /// Die Aufdeckfrage überlebt den Weg durch ein Bündel - samt Bild, Flächen und
        /// Betriebsart.
        /// </summary>
        [TestMethod]
        public void ARevealQuestionSurvivesTheBundle()
        {
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Bilder" };

            var frage = new RevealQuestion
            {
                Id = Guid.NewGuid(),
                CategoryId = kategorie.Id,
                Designation = "Wer ist das?",
                QuestionText = "Wer ist auf dem Bild?",
                Mode = RevealMode.Areas,
                ImageFileName = "portraet.png",
                AreasJson = RevealAreas.ToJson(DreiFlaechen),
                BlurStart = 33,
            };

            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Bilderabend" };

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(), QuestionBaseId = frage.Id,
            });

            var dokument = GameExportMapper.ToDocument(
                spiel, [frage], [kategorie], null, _ => "bild-1");

            dokument.Medien.Add(new GameExportDocument.MediaData
            {
                Schluessel = "bild-1",
                DateiImBuendel = "medien/bild-1.png",
                Endung = ".png",
            });

            var json = GameExportSerializer.ToJson(dokument);

            using var strom = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

            var bausatz = GameExportMapper.ToEntities(
                GameExportSerializer.Read(strom), _ => "neuer-name.png");

            var zurueck = bausatz.Fragen.Single() as RevealQuestion;

            Assert.IsNotNull(zurueck,
                "Aus dem Buendel kam keine Aufdeckfrage zurueck, sondern ein anderer Typ.");

            Assert.AreEqual(RevealMode.Areas, zurueck!.Mode);
            Assert.AreEqual(33, zurueck.BlurStart);
            Assert.AreEqual(3, RevealAreas.Parse(zurueck.AreasJson).Count,
                "Die Flaechen kamen nicht mit - im Spiel waere das Bild von Anfang an frei.");

            Assert.AreEqual("neuer-name.png", zurueck.ImageFileName,
                "Das Bild traegt nicht den Namen, den das Zielsystem vergeben hat.");
        }

        /// <summary>Es gibt genau ein Profil je Fragetyp, und die Aufdeckfrage hat eines.</summary>
        [TestMethod]
        public void EveryQuestionTypeHasExactlyOneProfile()
        {
            var typen = Enum.GetValues<QuestionType>();

            Assert.AreEqual(typen.Length, QuestionTypeProfiles.All.Count,
                "Es gibt nicht genau ein Profil je Fragetyp.");

            foreach (var typ in typen)
            {
                Assert.AreEqual(typ, QuestionTypeProfiles.For(typ).Typ,
                    $"Fuer {typ} kommt ein fremdes Profil zurueck.");
            }

            Assert.AreEqual("Aufdeckfrage", QuestionTypeProfiles.For(QuestionType.Reveal).DisplayName);

            // Und die Fabrik baut wirklich den Typ, der draufsteht.
            foreach (var typ in typen)
            {
                Assert.AreEqual(typ, Quizzer.DataModels.Helpers.Factory.CreateNewQuestion(typ).Typ,
                    $"Die Fabrik baut fuer {typ} einen anderen Typ.");
            }
        }
    }
}
