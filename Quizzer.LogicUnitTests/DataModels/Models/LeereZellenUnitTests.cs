using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.LogicUnitTests.DataModels.Models
{
    /// <summary>
    /// Ein Raster, das größer ist als die Zahl der zugewiesenen Fragen.
    /// <para>
    /// <b>Der Rasteraufbau legt für jede Position eine Zeile an</b> - auch für die leeren
    /// (<c>GridBuilder</c>, „Ensure full matrix exists"). Eine unbelegte Stelle ist damit ein
    /// vollwertiger Datensatz, und wer Zellen zählt, zählt sie mit.
    /// </para>
    /// <para>
    /// <b>Gemessen an der Spieldatenbank am 2026-09-07:</b> „Test Spiel" 25 Zellen davon 19 ohne
    /// Frage, „1" 9 davon 4. Beide Spiele konnten nie fertig werden - keine Siegerehrung, kein
    /// Phasenwechsel, und die Leiste meldete bis zuletzt „Noch 19 offen".
    /// </para>
    /// </summary>
    [TestClass]
    public class LeereZellenUnitTests
    {
        private static Game Spiel(int belegt, int leer, int phasen = 3)
        {
            var spiel = new Game
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                Phase = 1,
                SuggestedPhases = phasen,
            };

            for (var i = 0; i < belegt; i++)
            {
                spiel.GameGridCoordinates.Add(new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    X = i,
                    Y = 0,
                    QuestionBaseId = Guid.NewGuid(),
                });
            }

            for (var i = 0; i < leer; i++)
            {
                spiel.GameGridCoordinates.Add(new GameGridCoordinate
                {
                    Id = Guid.NewGuid(),
                    GameId = spiel.Id,
                    X = i,
                    Y = 1,
                    QuestionBaseId = null,
                });
            }

            return spiel;
        }

        /// <summary>Eine Zelle ohne Frage ist keine spielbare Zelle.</summary>
        [TestMethod]
        public void AnEmptyCellIsNotPlayable()
        {
            var spiel = Spiel(belegt: 6, leer: 19);

            Assert.AreEqual(25, spiel.GameGridCoordinates.Count,
                "Das Testraster hat gar keine leeren Zellen - dann misst die Probe nichts.");

            Assert.AreEqual(6, spiel.SpielbareZellen.Count(),
                "Leere Zellen zaehlen als spielbar mit.");
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn
        /// <c>SpielbareZellen</c> gar nichts mehr durchließe - dann wäre jedes Spiel sofort zu
        /// Ende.
        /// </summary>
        [TestMethod]
        public void ACellWithAQuestionCountsAsPlayable()
        {
            var spiel = Spiel(belegt: 4, leer: 0);

            Assert.AreEqual(4, spiel.SpielbareZellen.Count(),
                "Auch belegte Zellen fallen heraus - dann waere jedes Spiel sofort vorbei.");

            // Auch der Weg ueber die Navigation statt der Kennung zaehlt.
            var ueberNavigation = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = null,
                QuestionBase = new DefaultQuestion { Id = Guid.NewGuid(), Designation = "Ueber Navigation" },
            };

            Assert.IsTrue(ueberNavigation.HatFrage,
                "Eine Zelle, deren Frage nur ueber die Navigation haengt, gilt als leer.");
        }

        /// <summary>
        /// Die Phasenschwellen rechnen aus den spielbaren Zellen - sonst sind sie unerreichbar.
        /// </summary>
        [TestMethod]
        public void ThresholdsAreComputedFromPlayableCellsOnly()
        {
            var spiel = Spiel(belegt: 6, leer: 19);

            spiel.CalculatetThreshold();

            // 6 spielbare Zellen, 3 Phasen -> span 2 -> Schwellen bei 2 und 4.
            CollectionAssert.AreEqual(new[] { 2, 4 }, spiel.PhaseTrashholds,
                "Die Schwellen rechnen aus allen 25 Zellen; bei 6 spielbaren Fragen kommt der "
                + "Rundenzaehler nie so weit, und der Phasenwechsel wird nie angeboten. "
                + "Gerechnet wurde: " + string.Join(", ", spiel.PhaseTrashholds));
        }

        /// <summary>
        /// Ohne eine einzige belegte Zelle gibt es keine Schwellen - und keine Division durch
        /// nichts.
        /// </summary>
        [TestMethod]
        public void AGridWithoutAnyQuestionHasNoThresholds()
        {
            var spiel = Spiel(belegt: 0, leer: 9);

            spiel.CalculatetThreshold();

            Assert.AreEqual(0, spiel.PhaseTrashholds.Count,
                "Ein Raster ohne jede Frage bekommt Phasenschwellen.");
        }
    }
}
