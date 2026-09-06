using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Transfer;

namespace Quizzer.LogicUnitTests.DataModels
{
    /// <summary>
    /// Ein Bündel ist ein Bauplan, kein Spielstand - und das wird am <b>Text</b> der Datei
    /// gemessen.
    /// <para>
    /// <b>Warum am Text und nicht am Ergebnis des Imports.</b> Ein Import, der ein Feld liest und
    /// verwirft, ergäbe dieselbe Datenbank - die Datei aber wandert weiter. Wer sie öffnet, liest
    /// dann, wie der Abend ausgegangen ist, wer wie viele Punkte hatte und welche Spieler
    /// mitgespielt haben. Das gehört nicht in etwas, das man weitergibt.
    /// </para>
    /// <para>
    /// <b>Diese Zusicherung hält die Ausnahmeliste der Vollständigkeitsprobe ehrlich.</b> Fasst
    /// jemand sie zu weit - nimmt etwa <c>State</c> hinein, um eine rote Zeile loszuwerden -,
    /// bleibt jene grün. Diese hier fällt.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameExportHistoryUnitTests
    {
        /// <summary>Ein Spiel, das schon einen ganzen Abend hinter sich hat.</summary>
        private static (Game Spiel, QuestionBase Frage, Category Kategorie, Guid SpielerId) GespieltesSpiel()
        {
            var spielerId = Guid.NewGuid();
            var kategorie = new Category { Id = Guid.NewGuid(), Designation = "Musik" };

            var frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                CategoryId = kategorie.Id,
                Designation = "Wer sang das?",
                QuestionText = "Wer sang das?",
                Points = 100,
                OwnerPlayerId = spielerId,
            };

            var spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Abend vom Samstag",

                // Alles, was zum Verlauf gehoert:
                State = GameState.Finished,
                CurrentRound = 12,
                Phase = 3,
                Restart = true,
                RegularChoosingPlayerId = spielerId,
                CurrentChoosingPlayerId = spielerId,
                ModeratorPlayerId = spielerId,
            };

            spiel.GameGridCoordinates.Add(new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                X = 0,
                Y = 0,
                QuestionBaseId = frage.Id,

                IsDone = true,
                Phase = 3,
                CurrentPoints = 900,
                CurrentMinusPoints = 450,
            });

            return (spiel, frage, kategorie, spielerId);
        }

        /// <summary>
        /// Kein Feld des Verlaufs und keine Spieler-Kennung steht im Text der Datei.
        /// </summary>
        [TestMethod]
        public void TheFileCarriesNoTraceOfThePlayedEvening()
        {
            var (spiel, frage, kategorie, spielerId) = GespieltesSpiel();

            var json = GameExportSerializer.ToJson(
                GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null));

            var funde = new List<string>();

            // Die Spieler-Kennung: sie stand an vier Stellen im Spiel und einer in der Frage.
            if (json.Contains(spielerId.ToString(), StringComparison.OrdinalIgnoreCase))
                funde.Add("die Kennung eines Mitspielers");

            foreach (var feld in new[]
                     {
                         "IsDone", "CurrentPoints", "CurrentMinusPoints", "CurrentRound",
                         "State", "Restart", "ChoosingPlayerId", "ModeratorPlayerId",
                         "OwnerPlayerId", "RowVersion",
                     })
            {
                if (json.Contains("\"" + feld + "\"", StringComparison.Ordinal))
                    funde.Add(feld);
            }

            // Auch die Ids der Entitaeten selbst haben in einem Bauplan nichts zu suchen.
            foreach (var id in new[] { spiel.Id, frage.Id, kategorie.Id })
            {
                if (json.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase))
                    funde.Add("eine Entitaets-Id");
            }

            Assert.AreEqual(0, funde.Count,
                "Das Buendel traegt den gespielten Abend mit sich. Wer die Datei oeffnet, liest "
                + "mit, wie er ausgegangen ist. Gefunden: " + string.Join(", ", funde.Distinct()));
        }

        /// <summary>
        /// Die Gegenrichtung: die Suche findet etwas, das wirklich drinsteht.
        /// <para>
        /// Ohne sie wäre die Zusicherung oben auch dann grün, wenn <c>json</c> leer wäre oder die
        /// Suche nie zuschlüge.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheSearchActuallyFindsWhatIsInTheFile()
        {
            var (spiel, frage, kategorie, _) = GespieltesSpiel();

            var json = GameExportSerializer.ToJson(
                GameExportMapper.ToDocument(spiel, [frage], [kategorie], null, _ => null));

            Assert.IsTrue(json.Contains("Abend vom Samstag", StringComparison.Ordinal),
                "Die Bezeichnung des Spiels steht nicht in der Datei - dann misst der Test oben "
                + "eine leere Zeichenfolge.");

            Assert.IsTrue(json.Contains("Wer sang das?", StringComparison.Ordinal),
                "Die Frage steht nicht in der Datei.");

            Assert.IsTrue(json.Contains("Musik", StringComparison.Ordinal),
                "Die Kategorie steht nicht in der Datei.");

            Assert.IsTrue(json.Contains("\"Points\"", StringComparison.Ordinal),
                "Die Punktevorgabe der Frage steht nicht in der Datei - die gehoert zum Bauplan, "
                + "im Gegensatz zu den erspielten Punkten.");
        }
    }
}
