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

        /// <summary>
        /// <b>F13.</b> Dieselbe Stärke gibt auf jeder Fläche gleich viel preis.
        /// <para>
        /// Das ist der ganze Sinn der Umstellung vom 2026-09-09: die Zahl der Klötzchen - und
        /// damit die preisgegebene Bildinformation - darf nicht mehr an der Fenstergröße hängen.
        /// Gemessen wird deshalb das <b>Verhältnis</b> Fläche zu Klotzkante, nicht die Kante.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheSameStrengthRevealsTheSameEverywhere()
        {
            // Vorschau, Fragefenster, Beamer - drei sehr verschiedene Breiten.
            double[] breiten = [400, 800, 1920];

            var bloecke = breiten
                .Select(b => b / RevealAreas.Anzeigestaerke(40, b))
                .ToArray();

            foreach (var zahl in bloecke)
            {
                Assert.AreEqual(RevealAreas.Bezugsbreite / 40, zahl, 0.0001,
                    "Die Blockzahl haengt noch an der Flaeche: "
                    + string.Join(", ", bloecke));
            }
        }

        /// <summary>
        /// Die Gegenprobe zur vorigen: <b>ohne</b> die Umrechnung wäre die Blockzahl verschieden.
        /// <para>
        /// <b>Sie ist der Beleg, dass die erste Zusicherung etwas misst.</b> Ohne sie bliebe
        /// jene auch dann grün, wenn <see cref="RevealAreas.Anzeigestaerke"/> die Breite gar
        /// nicht mehr einrechnete - denn eine Konstante erfüllt sie ebenfalls.
        /// </para>
        /// </summary>
        [TestMethod]
        public void WithoutTheConversionTheProjectorWouldRevealMore()
        {
            // Genau die alte Rechnung: die Staerke ging roh als Punkte der Anzeige hinein.
            var vorschau = 400 / 40.0;
            var beamer = 1920 / 40.0;

            Assert.IsTrue(beamer > vorschau * 4,
                "Die alte Rechnung gab dem Beamer nicht mehr Bloecke - dann war der Befund, "
                + "aus dem F13 entstand, falsch.");

            Assert.AreEqual(
                1920 / 400.0,
                RevealAreas.Anzeigestaerke(40, 1920) / RevealAreas.Anzeigestaerke(40, 400),
                0.0001,
                "Die Staerke muss proportional zur Flaeche mitwachsen - genau das haelt die "
                + "Blockzahl konstant.");
        }

        /// <summary>Ohne Stärke oder ohne ausgelegte Breite gibt es nichts umzurechnen.</summary>
        [TestMethod]
        public void NothingToConvertStaysZero()
        {
            Assert.AreEqual(0, RevealAreas.Anzeigestaerke(0, 800), "Staerke 0 heisst scharf.");
            Assert.AreEqual(0, RevealAreas.Anzeigestaerke(40, 0), "Ohne Flaeche kein Bezug.");
        }
    }
}
